using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using File = System.IO.File;

namespace Cute.Commands.Type;

public class TypeDiffCommand(IConsoleWriter console, ILogger<TypeDiffCommand> logger, AppSettings appSettings,
    HttpClient httpClient)
    : BaseLoggedInCommand<TypeDiffCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type-id <ID>")]
        [Description("Specifies the content type id to generate types for. Default is all.")]
        public string? ContentTypeId { get; set; } = null!;

        [CommandOption("--source-environment-id")]
        [Description("Specifies the source environment id to do comparison against")]
        public string? SourceEnvironmentId { get; set; } = default!;
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var sourceEnvOptions = new OptionsForEnvironmentProvider(AppSettings, settings.SourceEnvironmentId!);

        var contentfulEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();

        var sourceEnvClient = new ContentfulConnection.Builder()
            .WithHttpClient(_httpClient)
            .WithOptionsProvider(sourceEnvOptions)
            .Build();

        var targetEnvId = (await ContentfulConnection.GetDefaultEnvironmentAsync()).SystemProperties.Id;

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {targetEnvId} <--> {settings.SourceEnvironmentId}", Globals.StyleHeading);

        List<ContentType> sourceEnvContentTypes = string.IsNullOrEmpty(settings.ContentTypeId)
            ? (await sourceEnvClient.GetContentTypesAsync()).OrderBy(ct => ct.Name).ToList()
            : [await sourceEnvClient.GetContentTypeAsync(settings.ContentTypeId)];

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{sourceEnvContentTypes.Count} found in environment {settings.SourceEnvironmentId}", Globals.StyleHeading);

        var allContentTypes = await ContentfulConnection.GetContentTypesAsync();

        List<ContentType> targetEnvContentTypes = string.IsNullOrEmpty(settings.ContentTypeId)
            ? allContentTypes.OrderBy(ct => ct.Name).ToList()
            : [await GetContentTypeOrThrowError(settings.ContentTypeId)];

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{targetEnvContentTypes.Count} found in environment {contentfulEnvironment.Id()}", Globals.StyleHeading);

        await CompareContentTypes(targetEnvContentTypes, sourceEnvContentTypes, settings.SourceEnvironmentId!);

        return 0;
    }

    private async Task CompareContentTypes(List<ContentType> targetEnvContentTypes, List<ContentType> sourceEnvContentTypes, string otherEnv)
    {
        var contentfulEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();

        var tmpTarget = Path.GetTempFileName() + ".cute-diff.json";
        var tmpSource = Path.GetTempFileName() + ".cute-diff.json";

        var tmpPath = Path.GetDirectoryName(tmpTarget);
        var oldFiles = Directory.GetFiles(tmpPath!, "*.cute-diff.json");
        foreach (var file in oldFiles)
        {
            File.Delete(file);
        }

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Extracting compare values from {contentfulEnvironment.Id()}...", Globals.StyleHeading);
        var targetObj = targetEnvContentTypes.Select(ExtractTypeInfo).ToList();

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Extracting compare values from {otherEnv}...", Globals.StyleHeading);
        var sourceObj = sourceEnvContentTypes.Select(ExtractTypeInfo).ToList();

        var now = DateTime.Now;

        var targetWrapper = new
        {
            Environment = contentfulEnvironment.Id(),
            RunDate = now,
            FileName = tmpTarget,
            Results = targetObj
        };

        File.WriteAllText(tmpTarget,
            JsonConvert.SerializeObject(targetWrapper, Formatting.Indented),
            Encoding.UTF8
        );

        var sourceWrapper = new
        {
            Environment = otherEnv,
            RunDate = now,
            FileName = tmpSource,
            Results = sourceObj
        };

        File.WriteAllText(tmpSource,
            JsonConvert.SerializeObject(sourceWrapper, Formatting.Indented),
            Encoding.UTF8
        );

        var exeFileName = FindExecutableInPath("code");
        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Found VS Code at {exeFileName}...", Globals.StyleHeading);

        string arguments = $"--diff \"{tmpTarget}\" \"{tmpSource}\"";

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Launching: {"code " + arguments}...", Globals.StyleHeading);

        var process = new Process();
        process.StartInfo.FileName = exeFileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        try
        {
            process.Start();
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            throw new CliException($"Error running VS Code ('code --diff'): {ex.Message}");
        }
    }

    private static string? FindExecutableInPath(string baseFileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var paths = pathEnv.Split(Path.PathSeparator);

        string[] extensions = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? [".exe", ".cmd", ".bat", ".com"]
            : [""]; // No extensions needed on Linux/Mac

        foreach (string path in paths)
        {
            foreach (string extension in extensions)
            {
                string fullPath = Path.Combine(path, baseFileName + extension);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    private JToken ExtractTypeInfo(ContentType c)
    {
        var obj = JToken.FromObject(c);

        HashSet<string> propertyNamesToRemove = [
          "Version",
          "Revision",
          "CreatedAt",
          "CreatedBy",
          "UpdatedAt",
          "UpdatedBy",
          "DeletedAt",
          "Locale",
          "ContentType",
          "Space",
          "PublishedCounter",
          "PublishedVersion",
          "PublishedBy",
          "PublishedAt",
          "PublishCounter",
          "FirstPublishedAt",
          "ArchivedAt",
          "ArchivedVersion",
          "ArchivedBy",
          "Status",
          "Environment",
          "Organization",
          "UsagePeriod",
        ];

        RemovePropertyRecursively(obj, propertyNamesToRemove);

        return obj;
    }

    private static void RemovePropertyRecursively(JToken token, HashSet<string> propertyNamesToRemove)
    {
        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            foreach (var propertyName in propertyNamesToRemove)
            {
                obj.Remove(propertyName);
            }

            foreach (var child in obj.Properties())
            {
                RemovePropertyRecursively(child.Value, propertyNamesToRemove);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            var array = (JArray)token;
            foreach (var item in array)
            {
                RemovePropertyRecursively(item, propertyNamesToRemove);
            }
        }
    }
}