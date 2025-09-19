using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeImportCommand(IConsoleWriter console, ILogger<TypeImportCommand> logger, AppSettings appSettings)
    : BaseLoggedInCommand<TypeImportCommand.Settings>(console, logger, appSettings)
{
    public class Settings : LoggedInSettings
    {

        [CommandOption("-p|--path <PATH>")]
        [Description("The output path and filename for the download operation")]
        public string Path { get; set; } = default!;
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        // Read the JSON from the saved file and deserialize to a list of KeyValuePair<string, ContentType>
        var readJson = await System.IO.File.ReadAllTextAsync(settings.Path);
        var deserializedList = JsonConvert.DeserializeObject<List<KeyValuePair<string, ContentType>>>(readJson);

        if(deserializedList == null || deserializedList.Count == 0)
        {
            throw new CliException("No content types found in the specified file.");
        }

        foreach (var item in deserializedList!)
        {
            _console.WriteNormal($"Importing content type '{item.Key}'...");
            await CreateContentTypeIfNotExist(item.Value);
        }

        _console.WriteBlankLine();
        _console.WriteAlert($"Done!");

        return 0;
    }
}