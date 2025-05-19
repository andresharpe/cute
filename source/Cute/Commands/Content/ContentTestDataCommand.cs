using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.CommandModels.ContentJoinCommand;
using Cute.Lib.Contentful.CommandModels.ContentSyncApi;
using Cute.Lib.Contentful.CommandModels.ContentTestData;
using Cute.Lib.Contentful.CommandModels.Schedule;
using Cute.Lib.InputAdapters.MemoryAdapters;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

using static Cute.Commands.Content.ContentTestDataCommand;

namespace Cute.Commands.Content;

public class ContentTestDataCommand(IConsoleWriter console, ILogger<ContentTestDataCommand> logger,
    AppSettings appSettings, HttpClient httpClient)
    : BaseLoggedInCommand<Settings>(console, logger, appSettings)
{
    public class Settings : LoggedInSettings
    {
        [CommandOption("-n|--number")]
        [Description("The number of user entries to generate. (default=1000).")]
        public int Number { get; set; } = 10;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var contentType = TestUserContentType.Instance();

        var contentTypeId = contentType.SystemProperties.Id;

        var defaultLocale = await ContentfulConnection.GetDefaultLocaleAsync();

        var defaultLocaleCode = defaultLocale.Code;

        var contentLocales = new ContentLocales([defaultLocaleCode], defaultLocaleCode);

        await CreateTestContentTypesIfNotExists();

        if (!ConfirmWithPromptChallenge($"{"DELETE"} and {"REGENERATE"} all entries in '{contentTypeId}'"))
        {
            return -1;
        }

        var testDataAdapter = new BogusInputAdapter(contentType, contentLocales, settings.Number);

        await PerformBulkOperations(
            [

                new DeleteBulkAction(ContentfulConnection, httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(contentLocales)
                    .WithVerbosity(settings.Verbosity),

                new UpsertBulkAction(ContentfulConnection, httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(contentLocales)
                    .WithNewEntries(testDataAdapter)
                    .WithVerbosity(settings.Verbosity)
                    .WithApplyChanges(true),

                new PublishBulkAction(ContentfulConnection, httpClient)
                    .WithContentType(contentType)
                    .WithContentLocales(contentLocales)
                    .WithVerbosity(settings.Verbosity)
                    .WithApplyChanges(true)

            ]
        );

        _console.WriteBlankLine();

        _console.WriteNormalWithHighlights($"Completed the {"REGENERATION"} of all '{contentTypeId}' entries.", Globals.StyleHeading);

        return 0;
    }

    private async Task CreateTestContentTypesIfNotExists()
    {
        if (await CreateContentTypeIfNotExist(CuteDataQueryContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type {"cuteDataQuery"}...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteLanguageContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteLanguage"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentSyncApiContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteContentSyncApi"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentGenerateContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteContentGenerate"}'...", Globals.StyleHeading);
        }
        if (await CreateContentTypeIfNotExist(CuteContentGenerateBatchContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type batch tracker '{"cuteContentGenerateBatch"}'...", Globals.StyleHeading);
        }
        if (await CreateContentTypeIfNotExist(CuteContentJoinType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"cuteContentJoin"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(TestUserContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"testUser"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(TestCountryContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"testCountry"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(TestLocationContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"testLocation"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(TestGeoContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"testGeo"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteScheduleContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"CuteSchedule"}'...", Globals.StyleHeading);
        }

        if (await CreateContentTypeIfNotExist(CuteContentTypeTranslationContentType.Instance()))
        {
            _console.WriteNormalWithHighlights($"Created content type '{"CuteContentTypeTranslation"}'...", Globals.StyleHeading);
        }
    }
}