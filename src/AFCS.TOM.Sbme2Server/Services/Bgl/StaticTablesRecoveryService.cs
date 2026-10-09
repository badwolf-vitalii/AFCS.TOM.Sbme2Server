using System.Data;
using System.Globalization;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using DL = AFCS.TOM.SbmeDataLayer;

namespace AFCS.TOM.Sbme2Server.Services.Bgl;

public sealed class StaticTablesRecoveryService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StaticTablesRecoveryService> _logger;
    
    private readonly IServiceScopeFactory _scopeFactory;

    public StaticTablesRecoveryService(IConfiguration configuration, IServiceScopeFactory scopeFactory, ILogger<StaticTablesRecoveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("StaticTablesRecovery");
        if (!section.GetValue("Enabled", false))
            return;

        if (section.GetValue("RestoreMissingRecordsOnly", true) == false)
        {
            _logger.LogError("Static table recovery only supports restoring missing records. Recovery is disabled.");
            return;
        }

        if (section.GetValue("RestoreOnStartup", true))
            await RunRecoverySafely(stoppingToken);

        var minutes = section.GetValue("CheckIntervalMinutes", 60);
        if (minutes <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunRecoverySafely(stoppingToken);
    }

    private async Task RunRecoverySafely(CancellationToken token)
    {
        try
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "StaticTables");
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Static tables directory not found: {folder}");

            // Hosted services are singletons; resolve a scoped DbContext for each recovery pass.
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DL.DataLayerContext>();
            await context.Database.OpenConnectionAsync(token);
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.json").OrderBy(x => x))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        await RestoreTable(context, file, token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Static table recovery failed for {File}", Path.GetFileName(file));
                    }
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Static table recovery failed; will retry at the next scheduled check");
        }
    }

    private async Task RestoreTable(DL.DataLayerContext context, string file, CancellationToken token)
    {
        var table = Path.GetFileNameWithoutExtension(file);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, token));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"Expected a JSON array in {file}.");

        var rows = document.RootElement.EnumerateArray().ToArray();
        if (rows.Length == 0)
        {
            _logger.LogInformation("Static table {Table} has no reference rows; skipping", table);
            return;
        }

        // Resolve actual schema, keys and insertable columns from the target database.
        const string metadataSql = @"
SELECT c.name, c.is_identity, c.is_computed, c.system_type_id,
       CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS is_pk,
       c.is_nullable, c.default_object_id, TYPE_NAME(c.user_type_id) AS type_name,
       c.max_length, c.precision, c.scale, c.is_identity
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
LEFT JOIN (
    SELECT ic.object_id, ic.column_id
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
WHERE s.name = 'dbo' AND t.name = @table
ORDER BY c.column_id;";

        // Use the connection managed by EF Core instead of creating a new SqlConnection.
        var connection = (SqlConnection)context.Database.GetDbConnection();

        var columns = new List<ColumnInfo>();
        await using (var command = new SqlCommand(metadataSql, connection))
        {
            command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = table;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                columns.Add(new ColumnInfo(
                    reader.GetString(0), reader.GetBoolean(1),
                    !reader.GetBoolean(2) && reader.GetByte(3) != 189,
                    reader.GetInt32(4) == 1, reader.GetBoolean(5),
                    reader.GetInt32(6) != 0, reader.GetString(7),
                    reader.GetInt16(8), reader.GetByte(9), reader.GetByte(10)));
        }

        if (columns.Count == 0)
            throw new InvalidOperationException($"Table dbo.{table} not found.");

        var keys = columns.Where(c => c.PrimaryKey).Select(c => c.Name).ToArray();
        if (keys.Length == 0)
            throw new InvalidOperationException($"Table dbo.{table} has no primary key.");

        var allowed = columns.Where(c => c.Insertable).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var required = columns.Where(c => c.Insertable && !c.Nullable && !c.HasDefault && !c.Identity)
            .Select(c => c.Name).ToArray();
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"An entry in {file} is not an object.");

            var names = row.EnumerateObject().Select(p => p.Name).ToArray();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length ||
                names.Any(n => !allowed.ContainsKey(n)) ||
                keys.Any(k => !names.Contains(k, StringComparer.OrdinalIgnoreCase)) ||
                required.Any(k => !names.Contains(k, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Invalid column set in {file}.");

            foreach (var property in row.EnumerateObject())
            {
                var column = allowed[property.Name];
                if (!column.Nullable && property.Value.ValueKind == JsonValueKind.Null)
                    throw new InvalidDataException($"Null value in non-nullable column {column.Name} of {file}.");
                // Convert every value before beginning the transaction.
                _ = ToSqlValue(property.Value, column);
            }

            var signature = string.Join("|", keys.Select(k =>
            {
                var property = row.EnumerateObject().First(p => p.Name.Equals(k, StringComparison.OrdinalIgnoreCase));
                if (property.Value.ValueKind == JsonValueKind.Null)
                    throw new InvalidDataException($"Null primary key in {file}.");
                return property.Value.GetRawText();
            }));
            if (!keySet.Add(signature))
                throw new InvalidDataException($"Duplicate primary key in {file}: {signature}");
        }

        var quotedTable = "[dbo].[" + table.Replace("]", "]]") + "]";
        var restored = 0;
        var needsIdentity = rows.Any(row => row.EnumerateObject().Any(p => allowed[p.Name].Identity));
        await using var efTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var transaction = (SqlTransaction)efTransaction.GetDbTransaction();
        var identityEnabled = false;
        try
        {
            if (needsIdentity)
            {
                await ExecuteIdentity(connection, transaction, quotedTable, true, token);
                identityEnabled = true;
            }

            foreach (var row in rows)
            {
                var properties = row.EnumerateObject().ToArray();
                var insert = properties.Where(p => allowed[p.Name].Insertable).ToArray();
                var conditions = string.Join(" AND ", keys.Select((k, i) =>
                    "[" + k.Replace("]", "]]") + "] = @key" + i));
                var insertColumns = string.Join(", ", insert.Select(p => "[" + p.Name.Replace("]", "]]") + "]"));
                if (insert.Length == 0)
                    throw new InvalidDataException($"No insertable columns in {file}.");
                var values = string.Join(", ", insert.Select((_, i) => "@value" + i));
                var sql = $"IF NOT EXISTS (SELECT 1 FROM {quotedTable} WITH (UPDLOCK, HOLDLOCK) WHERE {conditions}) " +
                    $"BEGIN INSERT INTO {quotedTable} ({insertColumns}) VALUES ({values}); SELECT 1; END ELSE SELECT 0;";
                await using var command = new SqlCommand(sql, connection, transaction);
                for (var i = 0; i < keys.Length; i++)
                {
                    var key = properties.First(p => p.Name.Equals(keys[i], StringComparison.OrdinalIgnoreCase));
                    AddParameter(command, "@key" + i, key.Value, allowed[key.Name]);
                }
                for (var i = 0; i < insert.Length; i++)
                    AddParameter(command, "@value" + i, insert[i].Value, allowed[insert[i].Name]);
                restored += Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            }

            if (identityEnabled)
            {
                await ExecuteIdentity(connection, transaction, quotedTable, false, CancellationToken.None);
                identityEnabled = false;
            }

            await efTransaction.CommitAsync(token);
            if (restored > 0)
                _logger.LogWarning("Restored {Count} missing records in dbo.{Table}", restored, table);
        }
        catch
        {
            if (identityEnabled)
            {
                try
                {
                    await ExecuteIdentity(connection, transaction, quotedTable, false, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to disable IDENTITY_INSERT for {Table}", table);
                    // Prevent a potentially contaminated connection from returning to the pool.
                    SqlConnection.ClearPool(connection);
                }
            }
            try
            {
                await efTransaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to roll back static table recovery for {Table}", table);
            }
            throw;
        }
    }

    private static async Task ExecuteIdentity(SqlConnection connection, SqlTransaction transaction, string table, bool enabled, CancellationToken token)
    {
        await using var command = new SqlCommand($"SET IDENTITY_INSERT {table} {(enabled ? "ON" : "OFF")}", connection, transaction);
        await command.ExecuteNonQueryAsync(token);
    }

    private sealed record ColumnInfo(string Name, bool Identity, bool Insertable, bool PrimaryKey,
        bool Nullable, bool HasDefault, string Type, short MaxLength, byte Precision, byte Scale);

    private static void AddParameter(SqlCommand command, string name, JsonElement json, ColumnInfo column)
    {
        var parameter = command.Parameters.Add(name, GetSqlType(column.Type));
        if (parameter.SqlDbType is SqlDbType.Decimal)
        {
            parameter.Precision = column.Precision;
            parameter.Scale = column.Scale;
        }
        if (parameter.SqlDbType is SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.Char
            or SqlDbType.Binary or SqlDbType.VarBinary)
            parameter.Size = column.MaxLength < 0 ? -1 : parameter.SqlDbType is SqlDbType.NVarChar or SqlDbType.NChar ? column.MaxLength / 2 : column.MaxLength;
        parameter.Value = ToSqlValue(json, column);
    }

    private static SqlDbType GetSqlType(string type) => type.ToLowerInvariant() switch
    {
        "bigint" => SqlDbType.BigInt,
        "int" => SqlDbType.Int,
        "smallint" => SqlDbType.SmallInt,
        "tinyint" => SqlDbType.TinyInt,
        "bit" => SqlDbType.Bit,
        "decimal" or "numeric" => SqlDbType.Decimal,
        "money" => SqlDbType.Money,
        "smallmoney" => SqlDbType.SmallMoney,
        "float" => SqlDbType.Float,
        "real" => SqlDbType.Real,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "date" => SqlDbType.Date,
        "datetime" => SqlDbType.DateTime,
        "datetime2" => SqlDbType.DateTime2,
        "smalldatetime" => SqlDbType.SmallDateTime,
        "datetimeoffset" => SqlDbType.DateTimeOffset,
        "time" => SqlDbType.Time,
        "char" => SqlDbType.Char,
        "nchar" => SqlDbType.NChar,
        "varchar" => SqlDbType.VarChar,
        "nvarchar" => SqlDbType.NVarChar,
        "text" => SqlDbType.Text,
        "ntext" => SqlDbType.NText,
        "binary" => SqlDbType.Binary,
        "varbinary" => SqlDbType.VarBinary,
        "image" => SqlDbType.Image,
        "xml" => SqlDbType.Xml,
        _ => throw new InvalidDataException($"Unsupported SQL column type: {type}")
    };

    private static object ToSqlValue(JsonElement value, ColumnInfo column)
    {
        if (value.ValueKind == JsonValueKind.Null)
            return DBNull.Value;

        if (value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            throw new InvalidDataException($"Expected scalar JSON for {column.Name}.");

        var type = GetSqlType(column.Type);
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        try
        {
            return type switch
            {
                SqlDbType.UniqueIdentifier => Guid.Parse(text),
                SqlDbType.Date or SqlDbType.DateTime or SqlDbType.DateTime2 or SqlDbType.SmallDateTime =>
                    DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                SqlDbType.DateTimeOffset => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Time => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Binary or SqlDbType.VarBinary or SqlDbType.Image =>
                    text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.FromHexString(text[2..]) : Convert.FromBase64String(text),
                SqlDbType.Bit => value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => bool.Parse(text)
                },
                SqlDbType.TinyInt => byte.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.SmallInt => short.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Int => int.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.BigInt => long.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney =>
                    decimal.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Real => float.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Float => double.Parse(text, CultureInfo.InvariantCulture),
                SqlDbType.Char or SqlDbType.NChar or SqlDbType.VarChar or SqlDbType.NVarChar
                    or SqlDbType.Text or SqlDbType.NText or SqlDbType.Xml => text,
                _ => throw new InvalidDataException($"Unsupported SQL type for {column.Name}.")
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new InvalidDataException($"Invalid value for column {column.Name} ({column.Type}).", ex);
        }
    }
}
