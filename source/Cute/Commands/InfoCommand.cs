using Cute.Constants;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Cute.Lib.Extensions;
using Cute.Config;
using Cute.Lib.Contentful;

namespace Cute.Commands;

public sealed class InfoCommand : LoggedInCommand<InfoCommand.Settings>
{
    public InfoCommand(IConsoleWriter console, ILogger<InfoCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings)
        : base(console, logger, contentfulConnection, appSettings)
    {
    }

    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        var topTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        topTable.AddColumn(new TableColumn(new Text("Space", Globals.StyleSubHeading)));
        topTable.AddColumn(new TableColumn(new Text("Id", Globals.StyleSubHeading)));

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
        typesTable.AddColumn(new TableColumn(new Text("Display Field", Globals.StyleSubHeading)));

        var localesTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        localesTable.AddColumn(new TableColumn(new Text("Name", Globals.StyleSubHeading)));
        localesTable.AddColumn(new TableColumn(new Text("Code", Globals.StyleSubHeading)));

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .StartAsync("Getting info...", async ctx =>
            {
                var space = this.ContentfulSpace;

                var contentTypes = (await ContentfulManagementClient.GetContentTypes(spaceId: ContentfulSpaceId))
                    .OrderBy(t => t.Name);

                foreach (var contentType in contentTypes)
                {
                    typesTable.AddRow(
                        new Markup(contentType.Name.RemoveEmojis().Trim().Snip(27), Globals.StyleNormal),
                        new Markup(contentType.SystemProperties.Id, Globals.StyleAlertAccent),
                        new Markup(contentType.Fields.Count.ToString(), Globals.StyleNormal).RightJustified(),
                        new Markup(contentType.DisplayField, Globals.StyleNormal)
                    );
                }

                var locales = (await ContentfulManagementClient.GetLocalesCollection(spaceId: ContentfulSpaceId))
                    .OrderBy(t => t.Name);

                foreach (var locale in locales)
                {
                    localesTable.AddRow(
                        new Markup(locale.Name, Globals.StyleNormal),
                        new Markup(locale.Code, Globals.StyleAlertAccent)
                    );
                }

                topTable.AddRow(
                    new Markup(space.Name, Globals.StyleAlert),
                    new Markup(ContentfulSpaceId, Globals.StyleNormal)
                );

                mainTable.AddRow(
                    typesTable,
                    localesTable
                );
            });

        AnsiConsole.Write(topTable);
        AnsiConsole.Write(mainTable);

        return 0;
    }
}