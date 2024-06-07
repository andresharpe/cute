using Contentful.Core.Errors;
using Cut.Commands;
using Cut.Constants;
using Cut.Lib.Exceptions;
using Cut.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using System.Runtime.InteropServices;
using System.Text;
using Yaml2Cf.Interceptors;

var exitValue = 0;

var isGettingVersion = (args.Length > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase));

if (!isGettingVersion)
{
    WriteBanner();
}

// Add services
var services = new ServiceCollection();
services.AddSingleton<IConsoleWriter, ConsoleWriter>();
services.AddSingleton(DataProtectionProvider.Create(Globals.AppName));
services.AddSingleton<IPersistedTokenCache, PersistedTokenCache>();

// Build cli app with DI
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName(Globals.AppName);

    config.PropagateExceptions();

    config.Settings.HelpProviderStyles = GetHelpProviderstyle();

    config.SetInterceptor(new OperatingSystemInterceptor());

    config.AddCommand<VersionCommand>("version")
        .WithDescription("Displays the current cut cli version.");

    config.AddCommand<AuthCommand>("auth")
        .WithDescription("Authenticates to a Contentful account.");

    config.AddCommand<InfoCommand>("info")
        .WithDescription("Display information about the default or specified space.");

    config.AddCommand<DownloadCommand>("download")
        .WithDescription("Downloads content from the default or specified space.");

    config.AddCommand<UploadCommand>("upload")
        .WithDescription("Uploads local content to the default or specified space.");
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
}

if (!isGettingVersion)
{
    AnsiConsole.WriteLine();
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
    cw.WriteDim($"version {VersionChecker.GetInstalledCliVersion()}");
    cw.WriteRuler();
    cw.WriteBlankLine();
}

static void WriteException(Exception ex)
{
    if (ex is ICliException
        || ex is CommandParseException
        || ex is ContentfulException
        || ex is CommandRuntimeException)

        AnsiConsole.Console.WriteLine($"Error: {ex.Message}", Globals.StyleAlert);
    else // something bigger and unhandled.
    {
        AnsiConsole.WriteException(ex, new ExceptionSettings
        {
            Format = ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks,
            Style = new ExceptionStyle
            {
                Exception = Globals.StyleDim,
                Message = Globals.StyleHeading,
                NonEmphasized = Globals.StyleDim,
                Parenthesis = Globals.StyleAlertAccent,
                Method = Globals.StyleAlert,
                ParameterName = Globals.StyleAlertAccent,
                ParameterType = Globals.StyleDim,
                Path = Globals.StyleAlert,
                LineNumber = Globals.StyleNormal,
                Dimmed = Globals.StyleDim,
            }
        });
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