using Cute.Constants;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using Cute.Lib.Extensions;

namespace Cute.Commands;

public class InfoCommand : LoggedInCommand<InfoCommand.Settings>
{
    public InfoCommand(IConsoleWriter console, IPersistedTokenCache tokenCache, ILogger<InfoCommand> logger)
        : base(console, tokenCache, logger)
    {
    }

    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var spaceTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        spaceTable.AddColumn(new TableColumn(new Text("Space", Globals.StyleSubHeading)));
        spaceTable.AddColumn(new TableColumn(new Text("Id", Globals.StyleSubHeading)));
        spaceTable.AddColumn(new TableColumn(new Text("Content Types", Globals.StyleSubHeading)));
        spaceTable.AddColumn(new TableColumn(new Text("Locales", Globals.StyleSubHeading)));

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
                var space = await ContentfulManagementClient.GetSpace(ContentfulSpaceId);

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

                spaceTable.AddRow(
                    new Markup(space.Name, Globals.StyleAlert),
                    new Markup(ContentfulSpaceId, Globals.StyleNormal),
                    typesTable,
                    localesTable
                );
            });

        AnsiConsole.Write(spaceTable);

        return 0;
    }
}