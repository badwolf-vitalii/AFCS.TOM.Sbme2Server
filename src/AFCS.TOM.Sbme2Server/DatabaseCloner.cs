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
        var loaded = false;

        if (File.Exists(staticTablesLstPath))
        {
            var tables = File.ReadAllLines(staticTablesLstPath);
            if (tables.Length > 0)
            {
                foreach (var table in tables)
                {
                    TryCopyTableData(sourceConnectionString, targetConnectionString, table);
                }

                loaded = true;
            }
        }

        if (!loaded)
        {
            TryCopyTableData(sourceConnectionString, targetConnectionString, "AgentShiftState");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "ApplicationShutdownReason");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "ArticleType");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "CardAnomalyType");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "CommandAttachmentType");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "ContactlessCardReissuingReason");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "CscType");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "DatabaseInfo");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "PaymentMethod");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "PtItemType");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "SbmeProfilePriceMapping");
            TryCopyTableData(sourceConnectionString, targetConnectionString, "SellingDataSendStatus");
        }
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

        var tableName = $"[{schema}].[{table}]";

        using var readCommand = new SqlCommand($"SELECT * FROM {tableName}", sourceConnection);
        using var reader = readCommand.ExecuteReader();

        using var bulkCopy = new SqlBulkCopy(
            targetConnection,
            SqlBulkCopyOptions.KeepIdentity,
            null) {
            DestinationTableName = tableName,
            BulkCopyTimeout = 0,
            BatchSize = 5000
        };

        bulkCopy.WriteToServer(reader);
    }
}