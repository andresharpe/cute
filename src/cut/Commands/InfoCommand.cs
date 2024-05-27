
using Spectre.Console.Cli;
using Cut.Services;
using Spectre.Console;
using Cut.Constants;

namespace Cut.Commands;

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

        if (result != 0 || _contentfulClient == null) return result;

        var spaceTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        spaceTable.AddColumn("Space");
        spaceTable.AddColumn("Id");
        spaceTable.AddColumn("Content Types");
        spaceTable.AddColumn("Locales");

        var typesTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        typesTable.AddColumn("Type Name");
        typesTable.AddColumn("Id");
        typesTable.AddColumn("Fields").RightAligned();
        typesTable.AddColumn("Display Field");

        var localesTable = new Table()
            .RoundedBorder()
            .BorderColor(Globals.StyleDim.Foreground);

        localesTable.AddColumn("Name");
        localesTable.AddColumn("Code");

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .StartAsync("Getting info...", async ctx =>
            {
                var space = await _contentfulClient.GetSpace(_spaceId);

                var contentTypes = (await _contentfulClient.GetContentTypes(spaceId: _spaceId))
                    .OrderBy(t => t.Name);

                foreach (var contentType in contentTypes)
                {
                    typesTable.AddRow(
                        new Markup(contentType.Name),
                        new Markup(contentType.SystemProperties.Id, Globals.StyleAlertAccent),
                        new Markup(contentType.Fields.Count.ToString()).RightJustified(),
                        new Markup(contentType.DisplayField)
                    );
                }

                var locales = (await _contentfulClient.GetLocalesCollection(spaceId: _spaceId))
                    .OrderBy(t => t.Name);

                foreach (var locale in locales)
                {
                    localesTable.AddRow(
                        new Markup(locale.Name),
                        new Markup(locale.Code, Globals.StyleAlertAccent)
                    );
                }

                spaceTable.AddRow(
                    new Markup(space.Name, Globals.StyleAlert), 
                    new Markup(_spaceId), 
                    typesTable,
                    localesTable
                );
            });
       
        AnsiConsole.Write(spaceTable);

        return 0;
    }

}