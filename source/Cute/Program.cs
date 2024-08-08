using Contentful.Core.Errors;
using Cute.Commands;
using Cute.Constants;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Exceptions;
using Cute.Services;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using System.ClientModel;
using System.Runtime.InteropServices;
using System.Text;
using Yaml2Cf.Interceptors;

// App flow vars

var exitValue = 0;

var isGettingVersion = args.Contains("version");

var isServer = args.Contains("--log-output"); // outputs logs and surpresses pretty console

// Get config from protected settings file and environment

var dataProtectionProvider = DataProtectionProvider.Create(Globals.AppName);

var appSettings = await new PersistedTokenCache(dataProtectionProvider).LoadAsync(Globals.AppName);

appSettings ??= new();

// Configure logging

var loggerConfig = new LoggerConfiguration()
    .Enrich.FromLogContext();

if (appSettings.OpenTelemetryEndpoint is not null && appSettings.OpenTelemetryApiKey is not null)
{
    loggerConfig.WriteTo.OpenTelemetry(x =>
    {
        x.Endpoint = appSettings.OpenTelemetryEndpoint;
        x.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
        x.Headers = new Dictionary<string, string> { ["X-Seq-ApiKey"] = appSettings.OpenTelemetryApiKey };
        x.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = Globals.AppName };
        x.RestrictedToMinimumLevel = LogEventLevel.Information;
    });
}

if (isServer)
{
    loggerConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: LogEventLevel.Information
    );
}

Log.Logger = loggerConfig.CreateLogger();

// Display banner

if (!isGettingVersion)
{
    WriteBanner();
}

// Surpress pretty console for webserver

if (isServer)
{
    ConsoleWriter.EnableConsole = false;
}

// Add services

var services = new ServiceCollection();
services.AddSingleton<IConsoleWriter, ConsoleWriter>();
services.AddSingleton<IPersistedTokenCache, PersistedTokenCache>();
services.AddSingleton(dataProtectionProvider);
services.AddSingleton(appSettings);
services.AddSingleton<IContentfulOptionsProvider>(appSettings);
services.AddTransient<AzureTranslator>();
services.AddTransient<ContentfulConnection>();
services.AddTransient<BulkActionExecutor>();
services.AddTransient<HttpResponseFileCache>();

services.AddHttpClient<AzureTranslator>();
services.AddHttpClient<ContentfulConnection>();
services.AddHttpClient<GetDataCommand>();
services.AddHttpClient<BulkActionExecutor>();

services.AddLogging(builder => builder.ClearProviders().AddSerilog());

// Build cli app with DI
var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

services.AddSingleton<ICommandApp>(app);

app.Configure(config =>
{
    config.SetApplicationName(Globals.AppName);

    config.PropagateExceptions();

    config.Settings.HelpProviderStyles = GetHelpProviderstyle();

    config.SetInterceptor(new OperatingSystemInterceptor());

    config.AddCommand<VersionCommand>("version")
        .WithDescription("Displays the current cut cli version.");

    config.AddCommand<LoginCommand>("login")
        .WithDescription("Login to a Contentful account.");

    config.AddCommand<InfoCommand>("info")
        .WithDescription("Display information about the default or specified space.");

    config.AddCommand<DownloadCommand>("download")
        .WithDescription("Downloads content from the default or specified space.");

    config.AddCommand<UploadCommand>("upload")
        .WithDescription("Uploads local content to the default or specified space.");

    config.AddCommand<GenerateCommand>("generate")
        .WithDescription("Use generative AI to help build drafts of your content.");

    config.AddCommand<JoinCommand>("join")
        .WithDescription("Join and generate content entries from multiple content types.");

    config.AddCommand<TypeGenCommand>("typegen")
        .WithDescription("Generate language types from Contentful content types.");

    config.AddCommand<BulkCommand>("bulk")
        .WithDescription("Unpublish, delete and purge all content type entries.");

    config.AddCommand<GetDataCommand>("getdata")
        .WithDescription("Sync Contentful content with WikiData.");

    config.AddCommand<WebserverCommand>("webserver")
        .WithDescription("Launch web server and listen to http requests and webhook calls.");

    config.AddCommand<EvaluateCommand>("evaluate")
        .WithDescription("Evaluate AI generated content, translations, and SEO quality using a set of metrics.");
});

try
{
    exitValue = await app.RunAsync(args);
}
catch (Exception ex)
{
    WriteException(ex);

    exitValue = -1;
}
finally
{
    await VersionChecker.CheckForLatestVersion();

    Log.Logger.Information("Exiting {app} (version {version})", Globals.AppLongName, Globals.AppVersion);
}

if (!isGettingVersion)
{
    var cw = new ConsoleWriter(AnsiConsole.Console);
    cw.WriteLine();
}

return exitValue;

static void WriteBanner()
{
    var cw = new ConsoleWriter(AnsiConsole.Console);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.OutputEncoding = Encoding.Unicode;
    }

    cw.WriteBlankLine();
    cw.WriteAlert(Globals.AppLongName);
    cw.WriteDim(Globals.AppMoreInfo);
    cw.WriteDim($"version {Globals.AppVersion}");
    cw.WriteRuler();
    cw.WriteBlankLine();

    Log.Logger.Information("Starting {app} (version {version})", Globals.AppLongName, Globals.AppVersion);
}

static void WriteException(Exception ex)
{
    var console = new ConsoleWriter(AnsiConsole.Console);

    if (ex is ICliException
        || ex is CommandParseException
        || ex is ContentfulException
        || ex is CommandRuntimeException
        || ex is HttpRequestException
        || ex is IOException
        || ex is ClientResultException)

        console.WriteLine($"Error: {ex.Message}", Globals.StyleAlert);
    else // something bigger and unhandled.
    {
        console.WriteException(ex);
    }
}

static HelpProviderStyle GetHelpProviderstyle()
{
    return new()
    {
        Arguments = new()
        {
            Header = Globals.StyleSubHeading,
            OptionalArgument = Globals.StyleDim,
            RequiredArgument = Globals.StyleAlertAccent,
        },

        Commands = new()
        {
            Header = Globals.StyleSubHeading,
            ChildCommand = Globals.StyleAlertAccent,
            RequiredArgument = Globals.StyleDim,
        },

        Options = new()
        {
            Header = Globals.StyleSubHeading,
            RequiredOption = Globals.StyleAlert,
            OptionalOption = Globals.StyleAlertAccent,
            DefaultValue = Globals.StyleDim,
            DefaultValueHeader = Globals.StyleNormal,
        },

        Description = new()
        {
            Header = Globals.StyleDim,
        },

        Usage = new()
        {
            Header = Globals.StyleSubHeading,
            Command = Globals.StyleAlertAccent,
            CurrentCommand = Globals.StyleAlert,
            Options = Globals.StyleDim,
        },

        Examples = new()
        {
            Header = Globals.StyleHeading,
            Arguments = Globals.StyleAlertAccent,
        }
    };
}