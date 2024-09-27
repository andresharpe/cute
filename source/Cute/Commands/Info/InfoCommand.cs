using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Extensions;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using static Cute.Commands.Info.InfoCommand;
using Table = Spectre.Console.Table;
using Text = Spectre.Console.Text;

namespace Cute.Commands.Info;

public sealed class InfoCommand(IConsoleWriter console, ILogger<InfoCommand> logger, AppSettings appSettings)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    public class Settings : LoggedInSettings
    {
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var topTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        topTable.AddColumn(new TableColumn(new Text("Space", Globals.StyleSubHeading)));
        topTable.AddColumn(new TableColumn(new Text("Id", Globals.StyleSubHeading)));
        topTable.AddColumn(new TableColumn(new Text("Environment", Globals.StyleSubHeading)));
        topTable.AddColumn(new TableColumn(new Text("User Id", Globals.StyleSubHeading)));
        topTable.AddColumn(new TableColumn(new Text("User Name", Globals.StyleSubHeading)));
        topTable.AddRow(
            new Markup((await ContentfulConnection.GetDefaultSpaceAsync()).Name, Globals.StyleAlert),
            new Markup((await ContentfulConnection.GetDefaultSpaceAsync()).Id(), Globals.StyleNormal),
            new Markup((await ContentfulConnection.GetDefaultEnvironmentAsync()).SystemProperties.Id, Globals.StyleNormal),
            new Markup((await ContentfulConnection.GetCurrentUserAsync()).Id(), Globals.StyleNormal),
            new Markup((await ContentfulConnection.GetCurrentUserAsync()).Email, Globals.StyleNormal)
        );
        AnsiConsole.Write(topTable);

        var mainTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        mainTable.AddColumn(new TableColumn(new Text("Content Types", Globals.StyleSubHeading)));
        mainTable.AddColumn(new TableColumn(new Text("Locales", Globals.StyleSubHeading)));

        var typesTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        typesTable.AddColumn(new TableColumn(new Text("Type Name", Globals.StyleSubHeading)));
        typesTable.AddColumn(new TableColumn(new Text("Content Id", Globals.StyleSubHeading)));
        typesTable.AddColumn(new TableColumn(new Text("Field #", Globals.StyleSubHeading))).RightAligned();
        typesTable.AddColumn(new TableColumn(new Text("Record #", Globals.StyleSubHeading))).RightAligned();

        var localesTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        localesTable.AddColumn(new TableColumn(new Text("Name", Globals.StyleSubHeading)));
        localesTable.AddColumn(new TableColumn(new Text("Code", Globals.StyleSubHeading)));

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .StartAsync("Getting info...", async ctx =>
            {
                var contentTypesExt = (await ContentfulConnection.GetContentTypeExtendedAsync())
                    .OrderBy(t => t.Name);

                foreach (var contentTypeExt in contentTypesExt)
                {
                    typesTable.AddRow(
                        new Markup(contentTypeExt.Name.RemoveEmojis().Trim().Snip(27), Globals.StyleNormal),
                        new Markup(contentTypeExt.Id(), Globals.StyleAlertAccent),
                        new Markup(contentTypeExt.Fields.Count.ToString(), Globals.StyleNormal).RightJustified(),
                        new Markup(contentTypeExt.TotalEntries.ToString(), Globals.StyleNormal).RightJustified()
                    );
                }

                var locales = (await ContentfulConnection.GetLocalesAsync())
                    .OrderBy(t => t.Name);

                foreach (var locale in locales)
                {
                    localesTable.AddRow(
                        new Markup(locale.Name, Globals.StyleNormal),
                        new Markup(locale.Code, Globals.StyleAlertAccent)
                    );
                }

                mainTable.AddRow(
                    typesTable,
                    localesTable
                );
            });

        AnsiConsole.Write(mainTable);

        return 0;
    }
}