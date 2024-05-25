

using Cut.Services;
using Spectre.Console;

var isGettingVersion = (args.Length > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase));

if (!isGettingVersion || args.Length == 0)
{
    var installedVersion = VersionChecker.GetInstalledCliVersion();

    AnsiConsole.MarkupLine(@$"[bold]yaml2cf[/] - Yaml to Contentful loader");
    AnsiConsole.MarkupLine(@$"[gray]version {installedVersion}[/]");
    AnsiConsole.MarkupLine(@$"");
}
