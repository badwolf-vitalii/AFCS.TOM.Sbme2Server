using AFCS.TOM.Sbme2Server;
using AFCS.TOM.Sbme2Server.Configurations;
using AFCS.TOM.Sbme2Server.Exceptions;
using AFCS.TOM.Sbme2Server.Services.Bgl;
using AFCS.TOM.Sbme2Server.Services.Dashboard;
using AFCS.TOM.Sbme2Server.Services.SBME;
using AFCS.TOM.SbmeDataLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NLog;
using System.Reflection;
using System.Text.Json.Serialization;
using Bgl = AFCS.TOM.Sbme2Server.Services.Bgl;
using Sbme = AFCS.TOM.Sbme2Server.Services.SBME;
using Sbme2 = AFCS.TOM.Sbme2Server.Services.SBME2;

//
var assembly = typeof(Program).Assembly;
var loc = assembly.Location;
var path = Path.GetDirectoryName(loc);
//

var _logger = LogManager.GetLogger("Sbme2Server");

//#if DEBUG
//await Task.Delay(30000);
//#endif

var version = Assembly.GetExecutingAssembly()?.GetName()?.Version;
if (version == null)
{
    _logger.Debug($"Starting server");
}
else
{
    _logger.Debug($"Starting server v. {version.Major}.{version.Minor}.{version.Build}.{version.Revision}");
}

if (args?.Any() ?? false)
{
    _logger.Debug($"Startup arguments: {string.Join(" ", args)}");
    var copy = args.ToList();
    var first = copy.FirstOrDefault(p => p?.Trim()?.StartsWith("key-", StringComparison.InvariantCultureIgnoreCase) ?? false);
    if (first != null)
    {
        copy.Remove(first);
        args = copy.ToArray();
        var keys = first?.ToLower()?.Replace("key-", string.Empty);
        if (!string.IsNullOrWhiteSpace(keys))
        {
            var split = keys.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => byte.TryParse(p, out _))
                .Select(p => byte.Parse(p))
                .ToArray();
            if (split.Length > 0)
            {
                KeysManager.LocalDbKeys = split;
            }
        }
    }
}

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();
var launchSettings = new LaunchSettings();
config.GetSection("LaunchSettings").Bind(launchSettings);

if (launchSettings != null)
{
    KeysManager.Enabled = launchSettings.Sbme1ServicesEnabled || launchSettings.Sbme2ServicesEnabled || (launchSettings.DsdeDashboardEnabled ?? false) || launchSettings.BglServicesEnabled;
}

ExHelper.AllowExceptionContainers = (bool)(config.GetValue(typeof(bool), "AllowExceptionContainers") ?? false);

var builder = WebApplication.CreateBuilder(args);
_logger.Debug($"ApplicationUrl: {launchSettings.ApplicationUrl}");
builder.WebHost.UseUrls(launchSettings.ApplicationUrl);

const string tnsnamesPath = "tnsnames";
if (Directory.Exists(tnsnamesPath))
{
    Oracle.ManagedDataAccess.Client.OracleConfiguration.TnsAdmin = tnsnamesPath;
}

// Add services to the container.
{
    AFCS.TOM.Sbme2Server.HelperClasses.JsonSerializer.DebugConfig = config.GetSection("DebuggingConfiguration")?.Get<DebuggingConfiguration>() ?? DebuggingConfiguration.Default;

    if (launchSettings.BglServicesEnabled)
    {
        var dataLayerConfiguration = config.GetSection("BglDataLayerConfiguration").Get<BglDataLayerConfiguration>();

        builder.Services.AddScoped<Bgl.IBglDbService, Bgl.BglDbService>();
#if DEBUG
        builder.Services.AddScoped<Bgl.IBglDbDebugService, Bgl.BglDbDebugService>();
#endif
        builder.Services.AddSingleton<IHostedService, Bgl.BacpacBackgroundService>();
        builder.Services.AddHostedService<Bgl.StaticTablesRecoveryService>();

        builder.Services.AddScoped<Bgl.ISalesService, Bgl.SalesService>();
        builder.Services.AddScoped<Bgl.IReceiptsService, Bgl.ReceiptsService>();
        builder.Services.AddScoped<IDsdeDashboardService, DsdeDashboardService>();
        builder.Services.AddScoped<KeysManager>();
        builder.Services.AddScoped<ReceiptsManager>();

        KeysManager.LocalDbKeyIndex = 0;
        var connectionString = string.Empty;
        var localDbKeys = KeysManager.LocalDbKeys;
        if (localDbKeys is { Length: > 0 })
        {
            _logger.Debug($"LocalDbKeys: {string.Join(",", localDbKeys)}");

            var validKeyFound = false;
            for (var index = 0; index < localDbKeys.Length; index++)
            {
                KeysManager.LocalDbKeyIndex = checked((byte)index);
                var key = localDbKeys[index];
                await using var dbctx = new DataLayerContext(dataLayerConfiguration.ConnectionString);
                try
                {
                    _logger.Debug($"Trying key #{key}");
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var dbVer = await dbctx.DatabaseInfos
                        .OrderByDescending(p => p.Version)
                        .FirstOrDefaultAsync(timeout.Token);
                    if (dbVer == null)
                    {
                        _logger.Warn($"Key #{key} - database information was not found");
                        continue;
                    }

                    _logger.Debug($"Key #{key} - OK");
                    validKeyFound = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, $"Key #{key} - NOT OK");
                }
            }

            if (!validKeyFound)
            {
                KeysManager.LocalDbKeyIndex = 0;
                const string message = "Unable to open the BGL database with any configured local database key.";
                _logger.Error(message);
                throw new InvalidOperationException(message);
            }
        }

        connectionString = dataLayerConfiguration.ConnectionString;
        builder.Services.AddScoped((sp) => { return new DataLayerContext(connectionString); });
        
        builder.Services.AddScoped<Bgl.ISalesThresholdsService, Bgl.SalesThresholdsService>();
    }
    else
    {
        builder.Services.AddScoped<Bgl.IBglDbService, Bgl.EmptyBglDbService>();
        builder.Services.AddScoped<Bgl.ISalesService, Bgl.EmptySalesService>();
        builder.Services.AddScoped<Bgl.IReceiptsService, Bgl.EmptyReceiptsService>();
        builder.Services.AddScoped<IDsdeDashboardService, EmptyDsdeDashboardService>();
    }

    if (launchSettings.Sbme1ServicesEnabled)
    {
        builder.Services.AddSingleton<Sbme.IAuthenticatorService, Sbme.AuthenticatorService>();
        if (!(AFCS.TOM.Sbme2Server.HelperClasses.JsonSerializer.DebugConfig?.NoSBME ?? false))
        {
            builder.Services.AddSingleton<Sbme.ICustomerService, Sbme.CustomerService>(); // SBME
            builder.Services.AddSingleton<IHostedService, SftpBackgroundService>();
            builder.Services.AddSingleton<IHostedService, SgContractsInfoCleanupBackgroundService>();
        }
        else
        {
            builder.Services.AddSingleton<Sbme.ICustomerService, Sbme.CustomerServiceNoSBME>(); // no SBMESBME
        }
    }
    else
    {
        builder.Services.AddSingleton<Sbme.IAuthenticatorService, Sbme.EmptyAuthenticatorService>();
        builder.Services.AddSingleton<Sbme.ICustomerService, Sbme.EmptyCustomerService>();
    }

    if (launchSettings.Sbme2ServicesEnabled)
    {
        builder.Services.AddSingleton<Sbme2.ICustomerService, Sbme2.CustomerService>();
        builder.Services.AddSingleton<Sbme2.IMediaService, Sbme2.MediaService>();
    }
    else
    {
        builder.Services.AddSingleton<Sbme2.ICustomerService, Sbme2.EmptyCustomerService>();
        builder.Services.AddSingleton<Sbme2.IMediaService, Sbme2.EmptyMediaService>();
    }

    builder.Services.AddControllers().AddNewtonsoftJson();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(p =>
    {
        p.CustomSchemaIds(type => type.ToString());
        p.SwaggerDoc("v1", new OpenApiInfo 
        { 
            Title = "AFCS WS",
            Version = Helper.AssemblyVer,
            Description =  "AFCS External Systems Interface"
        });
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        try
        {
            p.IncludeXmlComments(xmlPath);
        }
        catch
        {
        }
    });
    builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ByteArrayConverter());
    });
}
builder.Logging.AddConfiguration(config.GetSection("LoggingConfiguration"));
builder.Logging.AddDebug();
//builder.Logging.AddNLog();

if ((launchSettings.DsdeDashboardEnabled ?? false) && (launchSettings.UseCors ?? false))
{
    builder.Services.AddSignalR();
    builder.Services.AddCors(options => {
        options
            .AddPolicy("CORSPolicy",
                builder => {
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
    });
}

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.RoutePrefix = string.Empty;
});
app.UseHttpsRedirection();
app.UseRouting();
if ((launchSettings.DsdeDashboardEnabled ?? false) && (launchSettings.UseCors ?? false))
    app.UseCors("CORSPolicy");
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    if (launchSettings.DsdeDashboardEnabled ?? false)
    {
        endpoints.MapHub<MessageHub>("/api/SignalR/DsdeDashboard");
    }
});
app.MapControllers();
await app.RunAsync();
//app.Run();
