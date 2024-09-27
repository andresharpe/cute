using Cute.Commands.Info;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Exceptions;
using Cute.Lib.SiteGen;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands._Legacy;

public sealed class SiteGenCommand : LoggedInCommand<SiteGenCommand.Settings>
{
    private readonly SiteGenerator _siteGenerator;

    public SiteGenCommand(IConsoleWriter console, ILogger<InfoCommand> logger,
        LegacyContentfulConnection contentfulConnection, AppSettings appSettings,
        SiteGenerator siteGenerator)
        : base(console, logger, contentfulConnection, appSettings)
    {
        _siteGenerator = siteGenerator;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-o|--output")]
        [Description("The local path to output the generated site to")]
        public string OutputPath { get; set; } = default!;

        [CommandOption("-a|--app-platform-id")]
        [Description("The local path to output the generated site to")]
        public string AppPlatformId { get; set; } = default!;
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

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _ = await base.ExecuteAsync(context, settings);

        var generator = _siteGenerator
            .WithDisplayAction(f => _console.WriteNormalWithHighlights(f, Globals.StyleHeading))
            .WithOutputPath(settings.OutputPath);

        await generator.Generate(settings.AppPlatformId, _appSettings.GetSettings());

        return 0;
    }
}