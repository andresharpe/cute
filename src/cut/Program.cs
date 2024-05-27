

using Cut.Commands;
using Cut.Constants;
using Cut.Exceptions;
using Cut.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Runtime.InteropServices;
using System.Text;

var exitValue = 0;

var isGettingVersion = (args.Length > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase));

if (!isGettingVersion)
{
    WriteBanner();
}

// Add services
var services = new ServiceCollection();
services.AddSingleton<IConsoleWriter, ConsoleWriter>();

// Build cli app with DI
var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("cut");

    config.PropagateExceptions();

    config.AddCommand<VersionCommand>("version")
        .WithDescription("Displays the current cut cli version.");
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

return exitValue;

static void WriteBanner()
{
    var cw = new ConsoleWriter(AnsiConsole.Console);

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Console.OutputEncoding = Encoding.Unicode;
    }

    cw.WriteRuler();
    cw.WriteHeading("Contentful Update Tool");
    cw.WriteBody("Bulk upload and download content to and from excel/csv/tsv/yaml/json/sql.");
    cw.WriteBody($"version {VersionChecker.GetInstalledCliVersion()}");
    cw.WriteRuler();
}

static void WriteException(Exception ex)
{
    if (ex is ICliException)

        AnsiConsole.Console.WriteLine($"Error: {ex.Message}", Globals.StyleAlert);
    
    else
    {
        AnsiConsole.WriteException(ex, new ExceptionSettings
        {
            Format = ExceptionFormats.ShortenEverything | ExceptionFormats.ShowLinks,
            Style = new ExceptionStyle
            {
                Exception = Globals.StyleBody,
                Message = Globals.StyleHeading,
                NonEmphasized = Globals.StyleBody,
                Parenthesis = Globals.StyleAlertAccent,
                Method = Globals.StyleAlert,
                ParameterName = Globals.StyleAlertAccent,
                ParameterType = Globals.StyleBody,
                Path = Globals.StyleAlert,
                LineNumber = Globals.StyleHeading,
            }
        });
    }
}