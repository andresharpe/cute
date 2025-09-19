using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeExportCommand(IConsoleWriter console, ILogger<TypeExportCommand> logger, AppSettings appSettings)
    : BaseLoggedInCommand<TypeExportCommand.Settings>(console, logger, appSettings)
{
    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("Specifies the content type ids to export.")]
        public string[] ContentTypeIds { get; set; } = null!;

        [CommandOption("-p|--path <PATH>")]
        [Description("The output path and filename for the download operation")]
        public string? Path { get; set; }

        [CommandOption("--include-dependencies")]
        [Description("Export all dependencies.")]
        public bool IncludeDependencies { get; set; } = false;
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentTypeIds = settings.ContentTypeIds;

        Dictionary<string, ContentType> exportedContentTypes = new Dictionary<string, ContentType>();
        List<string> orderedContentTypeIds = new List<string>();

        foreach (var contentTypeId in contentTypeIds)
        {
            try
            {
                var contentType = await GetContentTypeOrThrowError(contentTypeId);
                contentType = await ContentfulConnection.CloneContentTypeAsync(contentType, contentTypeId, false);

                exportedContentTypes.Add(contentTypeId, contentType);
                orderedContentTypeIds.Add(contentTypeId);
            }
            catch
            {
                _console.WriteAlert($"Content type '{contentTypeId}' not found.");
            }
        }

        if (settings.IncludeDependencies)
        {
            var dependencies = await GetDependencies(exportedContentTypes, orderedContentTypeIds);
            while (dependencies.Any())
            {
                foreach (var dependency in dependencies)
                {
                    exportedContentTypes.Add(dependency.Key, dependency.Value);
                }
                dependencies = await GetDependencies(exportedContentTypes, orderedContentTypeIds);
            }
        }

        if (!exportedContentTypes.Any())
        {
            throw new CliException("No valid content types specified.");
        }

        List<KeyValuePair<string, ContentType>> orderedExportedContentTypes = new List<KeyValuePair<string, ContentType>>();
        for (var i = orderedContentTypeIds.Count - 1; i >= 0; i--)
        {
            var contentTypeId = orderedContentTypeIds[i];
            orderedExportedContentTypes.Add(new KeyValuePair<string, ContentType>(contentTypeId, exportedContentTypes[contentTypeId]));
        }

        // Serialize and save to file
        var json = JsonConvert.SerializeObject(orderedExportedContentTypes, Formatting.Indented);
        var outputPath = !string.IsNullOrWhiteSpace(settings.Path) ? settings.Path : "TypesExport.json";
        
        await System.IO.File.WriteAllTextAsync(outputPath, json);

        _console.WriteBlankLine();
        _console.WriteAlert($"Done! Exported to '{outputPath}'.");

        return 0;
    }

    private async Task<Dictionary<string, ContentType>> GetDependencies(Dictionary<string, ContentType> exportedContentTypes, List<string> orderedContentTypeIds)
    {
        Dictionary<string, ContentType> dependencies = new Dictionary<string, ContentType>();
        foreach (var exportedContentType in exportedContentTypes)
        {
            foreach (var field in exportedContentType.Value!.Fields)
            {
                List<string>? dependencyContentTypeIds = null;
                if (field.Type == "Link")
                {
                    dependencyContentTypeIds = field.Validations.OfType<LinkContentTypeValidator>().FirstOrDefault()?.ContentTypeIds;
                }
                if(field.Type == "Array" && field.Items?.Type == "Link")
                {
                    dependencyContentTypeIds = field.Items.Validations.OfType<LinkContentTypeValidator>().FirstOrDefault()?.ContentTypeIds;
                }

                if (dependencyContentTypeIds == null || dependencyContentTypeIds.Count == 0)
                {
                    continue;
                }
                foreach (var contentTypeId in dependencyContentTypeIds)
                {
                    if (exportedContentTypes.ContainsKey(contentTypeId) || dependencies.ContainsKey(contentTypeId))
                    {
                        continue;
                    }

                    try
                    {
                        var contentType = await GetContentTypeOrThrowError(contentTypeId);
                        contentType = await ContentfulConnection.CloneContentTypeAsync(contentType, contentTypeId, false);

                        dependencies.Add(contentTypeId, contentType);
                        orderedContentTypeIds.Add(contentTypeId);
                    }
                    catch
                    {
                        _console.WriteAlert($"Dependency content type '{contentTypeId}' not found. {exportedContentType.Key}->{field.Id}");
                    }
                }
            }
        }

        return dependencies;
    }
}