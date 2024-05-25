

using Cut.Services;
using Spectre.Console;

var isGettingVersion = (args.Length > 0 && args[0].Equals("version", StringComparison.OrdinalIgnoreCase));
var exitValue = 0;

if (!isGettingVersion || args.Length == 0)
{
    var installedVersion = VersionChecker.GetInstalledCliVersion();

    AnsiConsole.Write(new Rule());
    AnsiConsole.MarkupLine(@$"[bold]Contentful Update Tool[/]");
    AnsiConsole.MarkupLine(@$"[gray]Bulk upload and download from excel/csv/tsv/yaml/json/sql.[/]");
    AnsiConsole.MarkupLine(@$"[gray]version {installedVersion}[/]");
    AnsiConsole.MarkupLine(@$"");
    AnsiConsole.Write(new Rule());
}

try
{
}
catch 
{

}
finally
{
    await VersionChecker.CheckForLatestVersion();
}

return exitValue;