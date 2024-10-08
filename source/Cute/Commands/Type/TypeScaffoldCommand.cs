﻿using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.TypeGenAdapter;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeScaffoldCommand(IConsoleWriter console, ILogger<TypeScaffoldCommand> logger,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<TypeScaffoldCommand.Settings>(console, logger, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to generate types for. Default is all.")]
        public string? ContentTypeId { get; set; } = null!;

        [CommandOption("-o|--output")]
        [Description("The local path to output the generated types to.")]
        public string OutputPath { get; set; } = default!;

        [CommandOption("-l|--language")]
        [Description("The language to generate types for (TypeScript/CSharp/Excel).")]
        public GenTypeLanguage Language { get; set; } = GenTypeLanguage.TypeScript!;

        [CommandOption("-n|--namespace")]
        [Description("The optional namespace for the generated type.")]
        public string? Namespace { get; set; } = default!;
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

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        List<ContentType> contentTypes;

        var allContentTypes = await ContentfulConnection.GetContentTypesAsync();

        if (settings.EnvironmentId is null)
        {
            contentTypes = settings.ContentTypeId == null
                ? allContentTypes.ToList()
                : [await GetContentTypeOrThrowError(settings.ContentTypeId)];
        }
        else
        {
            var envOptions = new OptionsForEnvironmentProvider(AppSettings, settings.EnvironmentId!);

            var envClient = new ContentfulConnection.Builder()
                .WithHttpClient(_httpClient)
                .WithOptionsProvider(envOptions)
                .Build();

            contentTypes = settings.ContentTypeId == null
                ? (await envClient.GetContentTypesAsync()).OrderBy(ct => ct.Name).ToList()
                : [await envClient.GetContentTypeAsync(settings.ContentTypeId)];
        }

        var displayActions = new DisplayActions
        {
            ConfirmWithPromptChallenge = ConfirmWithPromptChallenge
        };

        ITypeGenAdapter adapter = TypeGenFactory.Create(settings.Language, displayActions);

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