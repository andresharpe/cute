using Cute.Constants;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Cute.Commands;

public class InfoCommand : LoggedInCommand<InfoCommand.Settings>
{
    public InfoCommand(IConsoleWriter console, IPersistedTokenCache tokenCache)
        : base(console, tokenCache)
    { }

    public class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        if (result != 0 || _contentfulManagementClient == null) return result;

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
                var space = await _contentfulManagementClient.GetSpace(_spaceId);

                var contentTypes = (await _contentfulManagementClient.GetContentTypes(spaceId: _spaceId))
                    .OrderBy(t => t.Name);

                foreach (var contentType in contentTypes)
                {
                    typesTable.AddRow(
                        new Markup(contentType.Name, Globals.StyleNormal),
                        new Markup(contentType.SystemProperties.Id, Globals.StyleAlertAccent),
                        new Markup(contentType.Fields.Count.ToString(), Globals.StyleNormal).RightJustified(),
                        new Markup(contentType.DisplayField, Globals.StyleNormal)
                    );
                }

                var locales = (await _contentfulManagementClient.GetLocalesCollection(spaceId: _spaceId))
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
                    new Markup(_spaceId, Globals.StyleNormal),
                    typesTable,
                    localesTable
                );
            });

        AnsiConsole.Write(spaceTable);

        return 0;
    }
}