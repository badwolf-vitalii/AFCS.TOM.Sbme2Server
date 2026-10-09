using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using NLog;
using System.Reflection;

namespace AFCS.TOM.Sbme2Server;
public static class DatabaseCloner
{
    private static Logger Logger => LogManager.GetLogger("BacpacLogger");

    public static void CreateDatabaseWithSameStructure(
        string server,
        string sourceDbName,
        string targetDbName,
        bool trustedConnection,
        string? user = null,
        string? password = null)
    {
        var connectionString = trustedConnection
            ? $"Server={server};Trusted_Connection=true;TrustServerCertificate=true;Encrypt=Optional;"
            : $"Server={server};Trusted_Connection=false;user={user};password=#PASSWORD#;TrustServerCertificate=true;Encrypt=Optional;";
        LogHelper.Info(Logger, $"CreateDatabaseWithSameStructure() with connection string: {connectionString}");
        connectionString = connectionString.Replace("#PASSWORD#", password);

        var oAssembly = Assembly.GetExecutingAssembly();
        var assemblyLocationDir = Path.GetDirectoryName(oAssembly.Location);
        var dir = Path.Combine(assemblyLocationDir!, "Bacpacs");
        var bacpacPath = Path.Combine(dir, "db_structure.bacpac");

        CreateDatabaseWithSameStructure(
            sourceConnectionString: connectionString + $"Database={sourceDbName};",
            targetConnectionString: connectionString + $"Database={targetDbName};",
            bacpacPath: bacpacPath);
    }

    public static void CreateDatabaseWithSameStructure(
        string sourceConnectionString,
        string targetConnectionString,
        string bacpacPath)
    {
        var source = new DacServices(sourceConnectionString);

        if (File.Exists(bacpacPath))
        {
            File.Delete(bacpacPath);
        }

        source.Extract(
            bacpacPath,
            databaseName: new SqlConnectionStringBuilder(sourceConnectionString).InitialCatalog,
            applicationName: "SchemaClone",
            applicationVersion: Version.Parse("1.0.0.0"),
            tables: null,
            extractOptions: new DacExtractOptions
            {
                ExtractAllTableData = false
            });

        var targetDbName = new SqlConnectionStringBuilder(targetConnectionString).InitialCatalog;

        var target = new DacServices(targetConnectionString);

        using var package = DacPackage.Load(bacpacPath);

        target.Deploy(
            package,
            targetDatabaseName: targetDbName,
            upgradeExisting: true);

        if (File.Exists(bacpacPath))
        {
            File.Delete(bacpacPath);
        }

        var assembly = typeof(Program).Assembly;
        var loc = assembly.Location;
        var path = Path.GetDirectoryName(loc);
        var staticTablesLstPath = Path.Combine(path!, "static_tables.lst");
        var tables = File.Exists(staticTablesLstPath)
            ? File.ReadAllLines(staticTablesLstPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .ToArray()
            : Array.Empty<string>();

        if (tables.Length == 0)
        {
            tables = new[]
            {
                "AgentShiftState",
                "ApplicationShutdownReason",
                "ArticleType",
                "CardAnomalyType",
                "CommandAttachmentType",
                "ContactlessCardReissuingReason",
                "CscType",
                "DatabaseInfo",
                "PaymentMethod",
                "PtItemType",
                "SbmeProfilePriceMapping",
                "SellingDataSendStatus"
            };
        }

        var failures = new List<Exception>();
        foreach (var table in tables)
        {
            try
            {
                CopyTableData(sourceConnectionString, targetConnectionString, table);
                LogHelper.Info(Logger, $"Copied static table dbo.{table}");
            }
            catch (Exception ex)
            {
                LogHelper.Error(Logger, ex);
                failures.Add(new InvalidOperationException($"Failed to copy static table dbo.{table}.", ex));
            }
        }

        if (failures.Count > 0)
            throw new AggregateException($"Failed to copy {failures.Count} of {tables.Length} static tables.", failures);
    }

    public static void TryCopyTableData(
        string sourceConnectionString,
        string targetConnectionString,
        string table,
        string schema = "dbo")
    {
        try
        {
            CopyTableData(sourceConnectionString, targetConnectionString, table, schema);
        }
        catch (Exception ex)
        {
            LogHelper.Error(Logger, ex);
            throw;
        }
    }

    public static void CopyTableData(
        string sourceConnectionString,
        string targetConnectionString,
        string table,
        string schema = "dbo")
    {
        using var sourceConnection = new SqlConnection(sourceConnectionString);
        using var targetConnection = new SqlConnection(targetConnectionString);

        sourceConnection.Open();
        targetConnection.Open();

        var tableName = $"[{schema.Replace("]", "]]")}].[{table.Replace("]", "]]")}]";

        using var readCommand = new SqlCommand($"SELECT * FROM {tableName}", sourceConnection);
        using var reader = readCommand.ExecuteReader();

        using var transaction = targetConnection.BeginTransaction();
        using var bulkCopy = new SqlBulkCopy(
            targetConnection,
            SqlBulkCopyOptions.KeepIdentity,
            transaction) {
            DestinationTableName = tableName,
            BulkCopyTimeout = 0,
            BatchSize = 5000
        };

        bulkCopy.WriteToServer(reader);
        transaction.Commit();
    }
}