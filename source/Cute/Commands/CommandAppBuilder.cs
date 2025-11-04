using Contentful.Core.Errors;
using Cute.Commands.App;
using Cute.Commands.Chat;
using Cute.Commands.Content;
using Cute.Commands.Eval;
using Cute.Commands.Info;
using Cute.Commands.Login;
using Cute.Commands.Logout;
using Cute.Commands.Server;
using Cute.Commands.Type;
using Cute.Commands.Version;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.AiModels;
using Cute.Lib.Cache;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.SiteGen;
using Cute.Services;
using Cute.Services.Translation;
using Cute.Services.Translation.Factories;
using Cute.Services.Translation.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using System.ClientModel;

namespace Cute.Commands;

public class CommandAppBuilder
{
    private readonly bool _isGettingVersion;
    private readonly bool _showBanner;
    private readonly bool _showLogOutput;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly AppSettings _appSettings;
    private readonly ServiceCollection _services;

    public bool IsGettingVersion => _isGettingVersion;
    public bool ShowBanner => _showBanner;
    public bool ShowLogOutput => _showLogOutput;

    public CommandAppBuilder(string[] args)
    {
        _isGettingVersion = args.Contains("version");

        _showBanner = !_isGettingVersion && !args.Contains("--no-banner") && !args.Contains("-x");

        _showLogOutput = args.Contains("--log-output");

        _dataProtectionProvider = DataProtectionProvider.Create(Globals.AppName);

        _appSettings = LoadAppSettings();

        ConfigureLogger();

        _services = BuildServiceCollection();
    }

    public CommandApp Build(Action<IConfigurator>? configurator = null)
    {
        var typeRegistrar = new TypeRegistrar(_services);

        var commandApp = new CommandApp(typeRegistrar);

        typeRegistrar.RegisterInstance(typeof(ICommandApp), commandApp);

        commandApp.Configure(commandConfig =>
        {
            commandConfig.SetApplicationName(Globals.AppName);

            commandConfig.PropagateExceptions();

            commandConfig.Settings.HelpProviderStyles = GetHelpProviderstyle();

            commandConfig.AddCommand<LoginCommand>("login")
                .WithDescription("Run this command first to configure your Contentful profile along with AI and Translation Services.");

            commandConfig.AddCommand<LogoutCommand>("logout")
                .WithDescription("Log out of Contentful and clear your session credentials.");

            commandConfig.AddCommand<InfoCommand>("info")
                .WithDescription("Display information about a Contentful space.");

            commandConfig.AddCommand<ChatCommand>("chat")
                .WithDescription("Make the robots do the work! Interact with your space using cute's AI assistant.");

            /*
            commandConfig.AddBranch("profile", branchConfig =>
            {
                branchConfig.SetDescription("Configure multiple profiles (space, environment, tokens) for connecting to contentful.");

                branchConfig.AddCommand<ProfileListCommand>("list")
                    .WithDescription("List all profiles.");

                branchConfig.AddCommand<ProfileAddCommand>("add")
                    .WithDescription("Add a new profile.");

                branchConfig.AddCommand<ProfileRemoveCommand>("remove")
                    .WithDescription("Remove a profile.");
            });
            */
            commandConfig.AddBranch("content", branchConfig =>
            {
                branchConfig.SetDescription("Manage your content entries in Contentful using bulk operations.");

                branchConfig.AddCommand<ContentDownloadCommand>("download")
                    .WithDescription("Download Contentful entries to a local CSV/TSV/YAML/JSON/Excel file.");

                branchConfig.AddCommand<ContentUploadCommand>("upload")
                    .WithDescription("Upload and sync Contentful entries from a local CSV/TSV/YAML/JSON/Excel file.");

                branchConfig.AddCommand<ContentEditCommand>("edit")
                    .WithDescription("Edit Contentful entries in bulk with an optional filter.");

                branchConfig.AddCommand<ContentReplaceCommand>("replace")
                    .WithDescription("Find and Replace values in Contentful entries in bulk with an optional filter.");

                branchConfig.AddCommand<ContentPublishCommand>("publish")
                    .WithDescription("Bulk publish all unpublished Contentful entries.");

                branchConfig.AddCommand<ContentUnpublishCommand>("unpublish")
                    .WithDescription("Unpublish all published Contentful entries.");

                branchConfig.AddCommand<ContentDeleteCommand>("delete")
                    .WithDescription("Unpublish and delete all Contentful entries.");

                branchConfig.AddCommand<ContentSyncApiCommand>("sync-api")
                    .WithDescription("Synchronise data to Contentful from an API.");

                branchConfig.AddCommand<ContentSeedGeoDataCommand>("seed-geo")
                    .WithDescription("Seed geographical test data to start your project.")
                    .IsHidden();

                branchConfig.AddCommand<ContentSeedGeoDataCommandLegacy>("seed-geo-legacy")
                    .WithDescription("Seed geographical test data to start your project.")
                    .IsHidden();

                branchConfig.AddCommand<ContentSyncDatabaseCommand>("sync-db")
                    .WithDescription("Synchronize data to Contentful from a database.")
                    .IsHidden();

                branchConfig.AddCommand<ContentGenerateCommand>("generate")
                    .WithDescription("Generate content using a Large Language Model (LLM).");

                branchConfig.AddCommand<ContentGenerateTestCommand>("generate-test")
                    .WithDescription("Test generation of content using a Large Language Model (LLM).");

                branchConfig.AddCommand<ContentTranslateCommand>("translate")
                    .WithDescription("Translate content using an LLM or Translation Service.");

                branchConfig.AddCommand<ContentTestDataCommand>("testdata")
                    .WithDescription("Generate test data.")
                    .IsHidden();

                branchConfig.AddCommand<ContentJoinCommand>("join")
                    .WithDescription("Join multiple content types to a destination content type.");

                branchConfig.AddCommand<ContentClearLocalizationCommand>("clear-fields")
                    .IsHidden()
                    .WithDescription("Clear localized fields for content type");

                branchConfig.AddCommand<ContentSetDefaultValueCommand>("set-default")
                    .WithDescription("Set default value for Contentful entries in bulk with an optional filter.");
            });

            commandConfig.AddBranch("type", branchConfig =>
            {
                branchConfig.SetDescription("Manage Contentful content types (models).");

                branchConfig.AddCommand<TypeScaffoldCommand>("scaffold")
                    .WithDescription("Automatically scaffold Typescript or c# classes from Contentful.");

                branchConfig.AddCommand<TypeDiffCommand>("diff")
                    .WithDescription("Compare content types across two environments and view with VS Code.");

                branchConfig.AddCommand<TypeCloneCommand>("clone")
                    .WithDescription("Clone a content type and its entries between environments.");

                branchConfig.AddCommand<TypeRenameCommand>("rename")
                    .WithDescription("Rename a content type including all references to it.");

                branchConfig.AddCommand<TypeDeleteCommand>("delete")
                    .WithDescription("Delete a content type along with its entries.");

                branchConfig.AddCommand<TypeExportCommand>("export")
                    .WithDescription("Export metadata for specified content types into a Json format.");

                branchConfig.AddCommand<TypeImportCommand>("import")
                    .WithDescription("Import metadata from previously exported Json file.");
            });

            commandConfig.AddBranch("app", branchConfig =>
            {
                branchConfig.SetDescription("Generate a website or app from your content space in Contentful.");

                branchConfig.AddCommand<AppGenerateCommand>("generate")
                 .WithDescription("Generate an app or website based on user configuration settings in Contentful.");
            });

            commandConfig.AddBranch("eval", branchConfig =>
            {
                branchConfig.SetDescription("Tools to evaluate the quality of generated content using the LLM and translation engine.");

                branchConfig.AddCommand<EvalContentGeneratorCommand>("content-generator")
                    .WithDescription("Use DeepEval to measure the quality of content generation.");

                branchConfig.AddCommand<EvalContentTranslatorCommand>("content-translator")
                    .WithDescription("Measure the quality of the translation engine output.");

                branchConfig.AddCommand<EvalNamingConventions>("naming")
                    .WithDescription("Check and remediate violations of site naming conventions.");
            });

            commandConfig.AddBranch("server", branchConfig =>
            {
                branchConfig.SetDescription("Run cute in server mode supporting scheduled tasks and webhooks.");

                branchConfig.AddCommand<ServerSchedulerCommand>("scheduler")
                    .WithDescription("Schedule and run cuteContentSyncApi entries.");

                branchConfig.AddCommand<ServerWebhooksCommand>("webhooks")
                    .WithDescription("Process callbacks from contentful.");
            });

            commandConfig.AddCommand<VersionCommand>("version")
                .WithDescription("Display the current installed version of the cute CLI.");

            configurator?.Invoke(commandConfig);
        });

        return commandApp;
    }

    private ServiceCollection BuildServiceCollection()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConsoleWriter, ConsoleWriter>();
        services.AddSingleton<IPersistedTokenCache, PersistedTokenCache>();
        services.AddSingleton(_dataProtectionProvider);
        services.AddSingleton(_appSettings);
        services.AddSingleton(_appSettings.GetSettings());
        services.AddSingleton<IContentfulOptionsProvider>(_appSettings);
        services.AddTransient<ContentfulConnection>();
        services.AddSingleton<IAzureOpenAiOptionsProvider>(_appSettings);
        services.AddSingleton<TranslateFactory>();
        services.AddTransient<AzureTranslator>();
        services.AddTransient<GPT4oTranslator>();
        services.AddTransient<GoogleTranslator>();
        services.AddTransient<DeeplTranslator>();

        services.AddTransient<HttpResponseFileCache>();
        services.AddTransient<SiteGenerator>();
        services.AddTransient<GenerateBulkAction>();
        services.AddHttpClient();

        services.AddLogging(builder => builder.ClearProviders().AddSerilog());

        return services;
    }

    private AppSettings LoadAppSettings()
    {
        var appSettings = new PersistedTokenCache(_dataProtectionProvider).LoadAsync(Globals.AppName).Result;

        return appSettings ??= new();
    }

    private void ConfigureLogger()
    {
        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext();

        if (_appSettings.OpenTelemetryEndpoint is not null && _appSettings.OpenTelemetryApiKey is not null)
        {
            loggerConfig.WriteTo.OpenTelemetry(o =>
            {
                o.Endpoint = _appSettings.OpenTelemetryEndpoint;
                o.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
                o.Headers = new Dictionary<string, string> { ["X-Seq-ApiKey"] = _appSettings.OpenTelemetryApiKey };
                o.ResourceAttributes = new Dictionary<string, object> { ["service.name"] = Globals.AppName };
                o.RestrictedToMinimumLevel = LogEventLevel.Information;
            });
        }

        if (_showLogOutput)
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information
            );

            ConsoleWriter.EnableConsole = false;
        }

        Log.Logger = loggerConfig.CreateLogger();

        return;
    }

    public void WriteException(Exception ex)
    {
        var console = new ConsoleWriter(AnsiConsole.Console);

        if (ex is ICliException
            || ex is CommandParseException
            || ex is ContentfulException
            || ex is CommandRuntimeException
            || ex is HttpRequestException
            || ex is IOException
            || ex is ClientResultException)
        {
            var message = ex.InnerException?.Message ?? ex.Message;

            console.WriteLine($"Error: {message}", Globals.StyleAlert);
        }
        else
        {
            console.WriteException(ex);
        }
    }

    private static HelpProviderStyle GetHelpProviderstyle()
    {
        return new()
        {
            Arguments = new()
            {
                Header = Globals.StyleSubHeading,
                OptionalArgument = Globals.StyleDim,
                RequiredArgument = Globals.StyleAlertAccent,
            },

            Commands = new()
            {
                Header = Globals.StyleSubHeading,
                ChildCommand = Globals.StyleAlertAccent,
                RequiredArgument = Globals.StyleDim,
            },

            Options = new()
            {
                Header = Globals.StyleSubHeading,
                RequiredOption = Globals.StyleAlert,
                // DefaultOption = Globals.StyleAlertAccent,  // Property renamed in newer Spectre.Console
                DefaultValue = Globals.StyleDim,
                DefaultValueHeader = Globals.StyleNormal,
            },

            Description = new()
            {
                Header = Globals.StyleDim,
            },

            Usage = new()
            {
                Header = Globals.StyleSubHeading,
                Command = Globals.StyleAlertAccent,
                CurrentCommand = Globals.StyleAlert,
                Options = Globals.StyleDim,
            },

            Examples = new()
            {
                Header = Globals.StyleHeading,
                Arguments = Globals.StyleAlertAccent,
            }
        };
    }
}