﻿using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Exceptions;
using Cute.Services;
using Cute.UiComponents;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using static Cute.Commands.Content.ContentGenerateCommand;

namespace Cute.Commands.Content;

public class ContentGenerateCommand(IConsoleWriter console, ILogger<ContentGenerateCommand> logger,
    ContentfulConnection contentfulConnection, AppSettings appSettings,
    GenerateBulkAction generateBulkAction)
    : BaseLoggedInCommand<Settings>(console, logger, contentfulConnection, appSettings)
{
    private readonly GenerateBulkAction _generBulkAction = generateBulkAction;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("The key of the 'cuteContentGenerate' entry.")]
        public string Key { get; set; } = default!;

        [CommandOption("-a|--apply")]
        [Description("Apply and publish all the required edits.")]
        public bool Apply { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Key))
        {
            return ValidationResult.Error("The key of the of the 'cuteContentGenerate' is required. Specify it with '-k' or '--key'.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentMetaType = CuteContentGenerateContentType.Instance();

        if (await CreateContentTypeIfNotExist(contentMetaType))
        {
            _console.WriteNormalWithHighlights($"Created content type {contentMetaType.SystemProperties.Id}...", Globals.StyleHeading);
        }

        var contentMetaTypeId = contentMetaType.SystemProperties.Id;

        var contentLocales = new ContentLocales([DefaultLocaleCode], DefaultLocaleCode);

        var apiSyncEntry = CuteContentGenerate.GetByKey(ContentfulClient, settings.Key)
            ?? throw new CliException($"No generate entry '{contentMetaTypeId}' with key '{settings.Key}' was found.");

        var displayActions = new DisplayActions()
        {
            DisplayNormal = _console.WriteNormal,
            DisplayFormatted = f => _console.WriteNormalWithHighlights(f, Globals.StyleHeading),
            DisplayAlert = _console.WriteAlert,
            DisplayDim = _console.WriteDim,
            DisplayHeading = _console.WriteHeading,
            DisplayRuler = _console.WriteRuler,
            DisplayBlankLine = _console.WriteBlankLine,
        };

        _generBulkAction
            .WithContentTypes(ContentTypes)
            .WithContentLocales(contentLocales);

        await ProgressBars.Instance().StartAsync(async ctx =>
        {
            var taskGenerate = ctx.AddTask($"[{Globals.StyleNormal.Foreground}]{Emoji.Known.Robot}  Generating[/]");

            await _generBulkAction.GenerateContent(settings.Key,
                displayActions,
                (step, steps) =>
                {
                    taskGenerate.MaxValue = steps;
                    taskGenerate.Value = step;
                }
            );

            taskGenerate.StopTask();
        });

        return 0;
    }
}