using Contentful.Core.Configuration;
using Contentful.Core.Models;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.TypeGenAdapter;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands;

public sealed class TypeGenCommand : LoggedInCommand<TypeGenCommand.Settings>
{
    private readonly ILogger<TypeGenCommand> _logger;
    private readonly HttpClient _httpClient;

    public TypeGenCommand(IConsoleWriter console, ILogger<TypeGenCommand> logger,
        ContentfulConnection contentfulConnection, AppSettings appSettings,
        HttpClient httpClient)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _logger = logger;

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

        [CommandOption("-l|--language")]
        [Description("The language to generate types for (TypeScript/CSharp)")]
        public GenTypeLanguage Language { get; set; } = GenTypeLanguage.TypeScript!;

        [CommandOption("-n|--namespace")]
        [Description("The optional namespace for the generated type")]
        public string? Namespace { get; set; } = default!;

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

        List<ContentType> contentTypes = settings.ContentType == "*"
            ? (await envClient.ManagementClient.GetContentTypes()).OrderBy(ct => ct.Name).ToList()
            : [await envClient.ManagementClient.GetContentType(settings.ContentType)];

        ITypeGenAdapter adapter = TypeGenFactory.Create(settings.Language);

        await adapter.PreGenerateTypeSource(contentTypes, settings.OutputPath, null, settings.Namespace);

        foreach (var contentType in contentTypes)
        {
            var fileName = await adapter.GenerateTypeSource(contentType, settings.OutputPath, null, settings.Namespace);

            _console.WriteNormal(fileName);
        }

        await adapter.PostGenerateTypeSource();

        return 0;
    }
}