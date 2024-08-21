using Contentful.Core.Models;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using File = System.IO.File;

namespace Cute.Commands;

public sealed class DiffCommand : LoggedInCommand<DiffCommand.Settings>
{
    private readonly HttpClient _httpClient;

    public DiffCommand(IConsoleWriter console, ILogger<TypeGenCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        HttpClient httpClient)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _httpClient = httpClient;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to generate types for. Default is all.")]
        public string? ContentType { get; set; } = null!;

        [CommandOption("-o|--output")]
        [Description("The local path to output the generated types to")]
        public string OutputPath { get; set; } = default!;

        [CommandOption("-e|--environment")]
        [Description("The optional namespace for the generated type")]
        public string? Environment { get; set; } = default!;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.OutputPath is null)
        {
            settings.OutputPath = Directory.GetCurrentDirectory();
        }
        else if (settings.OutputPath is not null)
        {
            if (Directory.Exists(settings.OutputPath))
            {
                var dir = new DirectoryInfo(settings.OutputPath);
                settings.OutputPath = dir.FullName;
            }
            else
            {
                throw new CliException($"Path {Path.GetFullPath(settings.OutputPath)} does not exist.");
            }
        }

        settings.ContentType ??= "*";

        settings.Environment ??= ContentfulEnvironmentId;

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var result = await base.ExecuteAsync(context, settings);

        var envOptions = new OptionsForEnvironmentProvider(_appSettings, settings.Environment!);

        var envClient = new ContentfulConnection(_httpClient, envOptions);

        _console.WriteNormalWithHighlights($"Comparing content types between environments: {ContentfulEnvironmentId} <--> {settings.Environment}", Globals.StyleHeading);

        List<ContentType> contentTypesEnv = settings.ContentType == "*"
            ? (await envClient.ManagementClient.GetContentTypes()).OrderBy(ct => ct.Name).ToList()
            : [await envClient.ManagementClient.GetContentType(settings.ContentType)];

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{contentTypesEnv.Count} found in environment {settings.Environment}", Globals.StyleHeading);

        List<ContentType> contentTypesMain = settings.ContentType == "*"
            ? (await ContentfulManagementClient.GetContentTypes()).OrderBy(ct => ct.Name).ToList()
            : [await ContentfulManagementClient.GetContentType(settings.ContentType)];

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{contentTypesMain.Count} found in environment {ContentfulEnvironmentId}", Globals.StyleHeading);

        await CompareContentTypes(contentTypesMain, contentTypesEnv, settings.Environment!);

        return 0;
    }

    private async Task CompareContentTypes(List<ContentType> contentTypesMain, List<ContentType> contentTypesEnv, string otherEnv)
    {
        var tmpMain = Path.GetTempFileName() + ".cute-diff.json";
        var tmpEnv = Path.GetTempFileName() + ".cute-diff.json";

        var tmpPath = Path.GetDirectoryName(tmpMain);
        var oldFiles = Directory.GetFiles(tmpPath!, "*.cute-diff.json");
        foreach (var file in oldFiles)
        {
            File.Delete(file);
        }

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Extracting compare values from {ContentfulEnvironmentId}...", Globals.StyleHeading);
        var mainObj = contentTypesMain.Select(ExtractTypeInfo).ToList();

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Extracting compare values from {otherEnv}...", Globals.StyleHeading);
        var envObj = contentTypesEnv.Select(ExtractTypeInfo).ToList();

        var now = DateTime.Now;

        var mainWrapper = new
        {
            Environment = ContentfulEnvironmentId,
            RunDate = now,
            FileName = tmpMain,
            Results = mainObj
        };

        File.WriteAllText(tmpMain,
            JsonConvert.SerializeObject(mainWrapper, Formatting.Indented),
            Encoding.UTF8
        );

        var envWrapper = new
        {
            Environment = otherEnv,
            RunDate = now,
            FileName = tmpEnv,
            Results = envObj
        };

        File.WriteAllText(tmpEnv,
            JsonConvert.SerializeObject(envWrapper, Formatting.Indented),
            Encoding.UTF8
        );

        var exeFileName = FindExecutableInPath("code");
        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Found VS Code at {exeFileName}...", Globals.StyleHeading);

        string arguments = $"--diff \"{tmpMain}\" \"{tmpEnv}\"";

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

        HashSet<string> propertyNames = [
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

        RemovePropertyRecursively(obj, propertyNames);

        return obj;
    }

    private static void RemovePropertyRecursively(JToken token, HashSet<string> propertyNames)
    {
        if (token.Type == JTokenType.Object)
        {
            var obj = (JObject)token;
            foreach (var propertyName in propertyNames)
            {
                obj.Remove(propertyName);
            }

            foreach (var child in obj.Properties())
            {
                RemovePropertyRecursively(child.Value, propertyNames);
            }
        }
        else if (token.Type == JTokenType.Array)
        {
            var array = (JArray)token;
            foreach (var item in array)
            {
                RemovePropertyRecursively(item, propertyNames);
            }
        }
    }
}