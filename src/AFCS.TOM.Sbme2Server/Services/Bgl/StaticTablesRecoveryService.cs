using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AFCS.TOM.Sbme2Server.Configurations;

namespace AFCS.TOM.Sbme2Server.Services.Bgl;

public sealed class StaticTablesRecoveryService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<StaticTablesRecoveryService> _logger;

    public StaticTablesRecoveryService(IConfiguration configuration, ILogger<StaticTablesRecoveryService> logger)
    {
        _configuration = configuration;
        _logger = logger;
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
            var db = _configuration.GetSection("BglDataLayerConfiguration").Get<BglDataLayerConfiguration>();
            if (db == null)
                throw new InvalidOperationException("BglDataLayerConfiguration is missing.");

            var folder = Path.Combine(AppContext.BaseDirectory, "StaticTables");
            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException($"Static tables directory not found: {folder}");

            await using var connection = new SqlConnection(db.ConnectionString);
            await connection.OpenAsync(token);

            foreach (var file in Directory.EnumerateFiles(folder, "*.json").OrderBy(x => x))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    await RestoreTable(connection, file, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Static table recovery failed for {File}", Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Static table recovery failed; will retry at the next scheduled check");
        }
    }

    private async Task RestoreTable(SqlConnection connection, string file, CancellationToken token)
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
       CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS is_pk
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

        var columns = new List<(string Name, bool Identity, bool Insertable, bool PrimaryKey)>();
        await using (var command = new SqlCommand(metadataSql, connection))
        {
            command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = table;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                columns.Add((reader.GetString(0), reader.GetBoolean(1),
                    !reader.GetBoolean(2) && reader.GetByte(3) != 189, reader.GetInt32(4) == 1));
        }

        if (columns.Count == 0)
            throw new InvalidOperationException($"Table dbo.{table} not found.");

        var keys = columns.Where(c => c.PrimaryKey).Select(c => c.Name).ToArray();
        if (keys.Length == 0)
            throw new InvalidOperationException($"Table dbo.{table} has no primary key.");

        var allowed = columns.Where(c => c.Insertable).ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var keySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"An entry in {file} is not an object.");

            var names = row.EnumerateObject().Select(p => p.Name).ToArray();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length ||
                names.Any(n => !allowed.ContainsKey(n)) ||
                keys.Any(k => !names.Contains(k, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Invalid column set in {file}.");

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
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, token);
        try
        {
            foreach (var row in rows)
            {
                var properties = row.EnumerateObject().ToArray();
                var insert = properties.Where(p => allowed[p.Name].Insertable).ToArray();
                var conditions = string.Join(" AND ", keys.Select((k, i) =>
                    "[" + k.Replace("]", "]]") + "] = @key" + i));
                var insertColumns = string.Join(", ", insert.Select(p => "[" + p.Name.Replace("]", "]]") + "]"));
                var values = string.Join(", ", insert.Select((_, i) => "@value" + i));
                var sql = $"IF NOT EXISTS (SELECT 1 FROM {quotedTable} WITH (UPDLOCK, HOLDLOCK) WHERE {conditions}) " +
                    $"BEGIN INSERT INTO {quotedTable} ({insertColumns}) VALUES ({values}); SELECT 1; END ELSE SELECT 0;";
                // IDENTITY_INSERT is session-scoped and must be disabled even on failure.
                var needsIdentity = insert.Any(p => allowed[p.Name].Identity);
                if (needsIdentity)
                    await ExecuteIdentity(connection, transaction, quotedTable, true, token);
                try
                {
                    await using var command = new SqlCommand(sql, connection, transaction);
                    for (var i = 0; i < keys.Length; i++)
                        command.Parameters.AddWithValue("@key" + i, ToSqlValue(properties.First(p => p.Name.Equals(keys[i], StringComparison.OrdinalIgnoreCase)).Value));
                    for (var i = 0; i < insert.Length; i++)
                        command.Parameters.AddWithValue("@value" + i, ToSqlValue(insert[i].Value));
                    restored += Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
                }
                finally
                {
                    if (needsIdentity)
                        await ExecuteIdentity(connection, transaction, quotedTable, false, token);
                }
            }

            await transaction.CommitAsync(token);
            if (restored > 0)
                _logger.LogWarning("Restored {Count} missing records in dbo.{Table}", restored, table);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task ExecuteIdentity(SqlConnection connection, SqlTransaction transaction, string table, bool enabled, CancellationToken token)
    {
        await using var command = new SqlCommand($"SET IDENTITY_INSERT {table} {(enabled ? "ON" : "OFF")}", connection, transaction);
        await command.ExecuteNonQueryAsync(token);
    }

    private static object ToSqlValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => DBNull.Value,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => value.TryGetInt64(out var integer) ? integer : value.GetDecimal(),
        JsonValueKind.String => value.GetString()!,
        _ => throw new InvalidDataException("Only scalar JSON values are supported.")
    };
}
