using Cute.Commands;
using Cute.Constants;
using Cute.Services;
using Serilog;
using Spectre.Console;
using System.Runtime.InteropServices;
using System.Text;

namespace Cute;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var exitValue = 0;

        // process common switches

        var cuteAppBuilder = new CommandAppBuilder(args);

        // Configure console for Windows

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.OutputEncoding = Encoding.Unicode;
        }

        // Display banner

        if (cuteAppBuilder.ShowBanner)
        {
            WriteAppBanner();
        }

        // Surpress pretty console for webserver

        if (cuteAppBuilder.ShowLogOutput)
        {
            ConsoleWriter.EnableConsole = false;
        }

        var cuteApp = cuteAppBuilder.Build();

        try
        {
            Log.Logger.Information("Starting {app} (version {version})", Globals.AppLongName, Globals.AppVersion);

            exitValue = await cuteApp.RunAsync(args);
        }
        catch (Exception ex)
        {
            cuteAppBuilder.WriteException(ex);

            exitValue = -1;
        }
        finally
        {
            await VersionChecker.CheckForLatestVersion();

            Log.Logger.Information("Exiting {app} (version {version})", Globals.AppLongName, Globals.AppVersion);
        }

        if (!cuteAppBuilder.IsGettingVersion)
        {
            var cw = new ConsoleWriter(AnsiConsole.Console);

            cw.WriteLine();
        }

        return exitValue;

        static void WriteAppBanner()
        {
            var cw = new ConsoleWriter(AnsiConsole.Console);

            cw.WriteBlankLine();
            cw.WriteAlert(Globals.AppLongName);
            cw.WriteDim(Globals.AppMoreInfo);
            cw.WriteDim($"version {Globals.AppVersion}");
            cw.WriteRuler();
            cw.WriteBlankLine();
        }
    }
}