using Azure.AI.OpenAI;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Extensions;
using Cute.Lib.AiModels;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Cute.Services.CliCommandInfo;
using Cute.Services.ClipboardWebServer;
using Cute.Services.Markdown;
using Cute.Services.ReadLine;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ClientModel;
using System.ComponentModel;
using System.Text;

namespace Cute.Commands.Chat;

public sealed class ChatCommand(IConsoleWriter console, ILogger<ChatCommand> logger,
    AppSettings appSettings, IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider)
    : BaseLoggedInCommand<ChatCommand.Settings>(console, logger, appSettings)
{
    private readonly IAzureOpenAiOptionsProvider _azureOpenAiOptionsProvider = azureOpenAiOptionsProvider;

    public class Settings : LoggedInSettings
    {
        [CommandOption("-k|--key")]
        [Description("Optional key for fetching a specific 'cuteContentGenerate' entry.")]
        public string Key { get; set; } = default!;

        [CommandOption("-p|--system-prompt")]
        [Description("System prompt to initialize the bot's starting context.")]
        public string SystemPrompt { get; set; } = default!;

        [CommandOption("-m|--max-tokens")]
        [Description("Maximum number of tokens (words) allowed in the bot's responses.")]
        public int MaxTokens { get; set; } = 1500;

        [CommandOption("-t|--temperature")]
        [Description("Controls randomness: higher values generate more creative responses.")]
        public float Temperature { get; set; } = 0.2f;

        [CommandOption("-f|--frequency-penalty")]
        [Description("Reduces repetition of frequently used phrases in bot responses.")]
        public float FrequencyPenalty { get; set; } = 0.1f;

        [CommandOption("--presence-penalty")]
        [Description("Discourages reusing phrases already present in the conversation.")]
        public float PresencePenalty { get; set; } = 0.1f;

        [CommandOption("--topP")]
        [Description("TopP controls diversity by limiting the token pool for bot responses.")]
        public float TopP { get; set; } = 0.85f;

        [CommandOption("--memory-length")]
        [Description("The total number of user and agent messages to keep in memory and send with new prompt.")]
        public int Memory { get; set; } = 16;

        [CommandOption("--douglas")]
        [Description("Summons Douglas. He knows most things about your Contentful space.")]
        public bool IsDouglas { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.MaxTokens < 1 || settings.MaxTokens > 4096)
        {
            return ValidationResult.Error("MaxTokens must be between 1 and 4096.");
        }

        if (settings.Temperature < 0 || settings.Temperature > 1)
        {
            return ValidationResult.Error("Temperature must be between 0 and 1.");
        }

        if (settings.FrequencyPenalty < 0 || settings.FrequencyPenalty > 2)
        {
            return ValidationResult.Error("FrequencyPenalty must be between 0 and 2.");
        }

        if (settings.PresencePenalty < 0 || settings.PresencePenalty > 2)
        {
            return ValidationResult.Error("PresencePenalty must be between 0 and 2.");
        }

        if (settings.TopP < 0 || settings.TopP > 1)
        {
            return ValidationResult.Error("TopP must be between 0 and 1.");
        }

        return base.Validate(context, settings);
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        string? systemMessage = null;

        bool isDouglas = settings.IsDouglas
            || (settings.Key == null && string.IsNullOrWhiteSpace(settings.SystemPrompt));

        ChatCompletionOptions? chatCompletionOptions = null;

        if (settings.Key != null)
        {
            var apiSyncEntry = ContentfulConnection.GetPreviewEntryByKey<CuteContentGenerate>(settings.Key)
                ?? throw new CliException($"No generate entry '{"cuteContentGenerate"}' with key '{settings.Key}' was found.");

            systemMessage = apiSyncEntry.SystemMessage;

            chatCompletionOptions = new ChatCompletionOptions();

            if (apiSyncEntry.MaxTokenLimit.HasValue)
            {
                chatCompletionOptions.MaxOutputTokenCount = apiSyncEntry.MaxTokenLimit;
            }
            if (apiSyncEntry.Temperature.HasValue)
            {
                chatCompletionOptions.Temperature = (float)apiSyncEntry.Temperature;
            }
            if (apiSyncEntry.FrequencyPenalty.HasValue)
            {
                chatCompletionOptions.FrequencyPenalty = (float)apiSyncEntry.FrequencyPenalty;
            }
            if (apiSyncEntry.PresencePenalty.HasValue)
            {
                chatCompletionOptions.PresencePenalty = (float)apiSyncEntry.PresencePenalty;
            }
            if (apiSyncEntry.TopP.HasValue)
            {
                chatCompletionOptions.TopP = (float)apiSyncEntry.TopP;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.SystemPrompt))
        {
            systemMessage = settings.SystemPrompt;
        }

        Space defaultSpace = default!;
        ContentfulEnvironment defaultEnvironment = default!;
        User currentUser = default!;
        IEnumerable<ContentType> contentTypes = default!;
        IEnumerable<Locale> locales = default!;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Aesthetic)
            .StartAsync("Getting info...", async ctx =>
            {
                defaultSpace = await ContentfulConnection.GetDefaultSpaceAsync();
                defaultEnvironment = await ContentfulConnection.GetDefaultEnvironmentAsync();
                currentUser = await ContentfulConnection.GetCurrentUserAsync();
                contentTypes = await ContentfulConnection.GetContentTypesAsync();
                locales = await ContentfulConnection.GetLocalesAsync();
            });

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"{(isDouglas ? SayHi() : "Hi")} {currentUser.FirstName},", Globals.StyleHeading);

        if (isDouglas)
        {
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"Greetings, traveler of the '{defaultSpace.Name}' space, wanderer of the '{defaultEnvironment.Id()}' environment!",
                Globals.StyleHeading);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"I'm Douglas, your guide to the wonders of Contentful.",
                Globals.StyleHeading);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"Whether you need to craft precise GraphQL queries, wield the power of the '{"cute"}' CLI, or simply",
                Globals.StyleHeading);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"explore the vast content types and fields, I'm here to assist.",
                Globals.StyleHeading);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"Remember, type '{"bye"}' or '{"exit"}' to bid me farewell. Ready to embark on this journey?",
                Globals.StyleHeading);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"Just type your prompt and press '{"<Tab>"}' or {"<Ctrl+Enter>"} to set sail!",
                Globals.StyleHeading);
            _console.WriteBlankLine();
        }

        var chatClient = CreateChatClient();

        chatCompletionOptions ??= CreateChatCompletionOptions(settings);

        systemMessage ??= GetSystemMessage(defaultSpace, defaultEnvironment, currentUser, contentTypes, locales);

        if (settings.Verbosity >= Verbosity.Detailed)
        {
            _console.WriteBlankLine();
            _console.WriteRuler("System Prompt");
            _console.WriteBlankLine();
            _console.WriteDim(systemMessage);
            _console.WriteBlankLine();
            _console.WriteRuler("Model Setings");
            _console.WriteBlankLine();
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"MaxOutputTokenCount : {chatCompletionOptions.MaxOutputTokenCount}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Temperature         : {chatCompletionOptions.Temperature}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"TopP                : {chatCompletionOptions.TopP}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Frequency Penalty   : {chatCompletionOptions.FrequencyPenalty}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Presence Penalty    : {chatCompletionOptions.PresencePenalty}", Globals.StyleDim, Globals.StyleSubHeading));
            _console.WriteBlankLine();
            _console.WriteRuler();
        }

        if (!isDouglas)
        {
            _console.WriteNormalWithHighlights($"Press {"<Tab>"} or {"<Ctrl+Enter>"} to submit your prompt.", Globals.StyleHeading);
        }

        List<ChatMessage> messages = [new SystemChatMessage(systemMessage)];

        // Start clipboard listener

        var cts = new CancellationTokenSource();
        Task clipboardServer = ClipboardServer.StartServerAsync(cts.Token);

        var prompt = new MultiLineConsoleInput.InputOptions()
        {
            Prompt = "> ",
            TextForeground = Globals.StyleInput.Foreground.ToSystemDrawingColor(),
            TextBackground = Globals.StyleInput.Background.ToSystemDrawingColor(),
            AllowBlankResult = false,
        };

        string lastContentInfoPromptAdded = string.Empty;
        string? autoInput = null;

        while (true)
        {
            if (messages.Count > settings.Memory)
            {
                var messagesToRemove = messages.Count - settings.Memory;
                // leave system message at element zero always!
                messages.RemoveRange(1, messagesToRemove);
            }

            _console.WriteBlankLine();

            var input = autoInput ?? MultiLineConsoleInput.ReadLine(prompt);

            if (string.IsNullOrWhiteSpace(input) || UserWantsToLeave(input))
            {
                _console.WriteBlankLine();
                _console.WriteAlert(isDouglas ? $"{SayBye()}!" : "Thank you for trying \"cute chat\". Good bye.");
                _console.WriteBlankLine();
                break;
            }

            if (autoInput is null)
            {
                DisplayPromptCopyLink(input);
            }

            autoInput = null;

            messages.Add(new UserChatMessage(input));

            string response = string.Empty;

            await AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBall)
            .StartAsync(isDouglas ? SayThinking() : "thinking...", async ctx =>
            {
                response = await SendPromptToModel(chatClient, chatCompletionOptions, messages);
            });

            var botResponse = isDouglas
                ? DeserializeBotResponse(response)
                : new BotResponse() { Answer = response };

            _console.WriteBlankLine();

            messages.Add(new AssistantChatMessage(response));

            if (botResponse.Answer is not null)
            {
                DisplayResponseCopyLink(botResponse.Answer);
                MarkdownConsole.Write(botResponse.Answer);

                if (isDouglas
                    && lastContentInfoPromptAdded != botResponse.ContentTypeId
                    && !string.IsNullOrWhiteSpace(botResponse.ContentTypeId))
                {
                    await BuildContentTypeGraphQLPromptInfo(botResponse);
                    if (botResponse.ContentInfo is not null)
                    {
                        messages.Add(new SystemChatMessage(botResponse.ContentInfo.ToString()));
                    }
                    lastContentInfoPromptAdded = botResponse.ContentTypeId;
                }
            }

            if (botResponse.Type == "Exit")
            {
                _console.WriteBlankLine();
                _console.WriteAlert(isDouglas ? $"{SayBye()}!" : "Thank you for trying \"cute chat\". Good bye.");
                _console.WriteBlankLine();
                break;
            }

            if (botResponse.Question is not null && botResponse.Question.Contains("Shall we give it a shot?"))
            {
                try
                {
                    await HandleExecution(locales, botResponse);
                }
                catch (Exception ex)
                {
                    autoInput = $"""
                        I got the following exception. Please fix?:
                        {ex.Message}
                        {ex.InnerException?.Message}
                        """;

                    try
                    {
                        var info = JsonConvert.DeserializeObject<JObject>(ex.Message);
                        if (info is not null)
                        {
                            _console.WriteDim(info.ToUserFriendlyString() ?? string.Empty);
                            _console.WriteBlankLine();
                        }
                    }
                    catch
                    { // ignore
                    }
                    _console.WriteAlert("The operation returned an error. Don't panic! (the details have been shared with Douglas)...");
                }
                continue;
            }

            if (botResponse.Question is not null)
            {
                _console.WriteBlankLine();
                MarkdownConsole.Write(botResponse.Question);
            }
        }

        cts.Cancel();

        await clipboardServer;

        return 0;
    }

    private static void DisplayPromptCopyLink(string input)
    {
        var headingBackground = Globals.StyleInput.Background.ToHex();
        var copyLinkColor = Globals.StyleInput.Foreground.ToHex();
        var headerPadding = AnsiConsole.Profile.Width - 4 - 2;
        var url = ClipboardServer.RegisterCopyText(input);
        AnsiConsole.MarkupLine($"  [default on #{headingBackground}]{"".PadRight(headerPadding)}[#{copyLinkColor} italic link={url}]Copy[/][/]");
    }

    private static void DisplayResponseCopyLink(string answer)
    {
        var headingBackground = Globals.StyleCodeHeading.Background.ToHex();
        var copyLinkColor = Globals.StyleSubHeading.Foreground;
        var headerPadding = AnsiConsole.Profile.Width - 4;
        var url = ClipboardServer.RegisterCopyText(answer);
        AnsiConsole.MarkupLine($"[default on #{headingBackground}]{"".PadRight(headerPadding)}[{copyLinkColor} italic link={url}]Copy[/][/]");
        AnsiConsole.WriteLine();
    }

    private static void DisplayQueryOrCommandCopyLink(string queryOCommand)
    {
        var copyLinkColor = Globals.StyleAlertAccent.Foreground;
        var headerPadding = AnsiConsole.Profile.Width - 4;
        var url = ClipboardServer.RegisterCopyText(queryOCommand);
        AnsiConsole.MarkupLine($"{"".PadRight(headerPadding)}[{copyLinkColor} italic link={url}]Copy[/]");
    }

    private async Task HandleExecution(IEnumerable<Locale> locales, BotResponse botResponse)
    {
        _console.WriteBlankLine();
        _console.WriteRuler("Solution");
        DisplayQueryOrCommandCopyLink(botResponse.QueryOrCommand);
        _console.WriteAlertAccent(botResponse.QueryOrCommand);
        _console.WriteBlankLine();

        var env = (await ContentfulConnection.GetDefaultEnvironmentAsync()).Id();
        var space = (await ContentfulConnection.GetDefaultSpaceAsync()).Name;
        var spaceConfirmation = $"[italic {Globals.StyleDim.Foreground}]...the query will run in the '[{Globals.StyleSubHeading.Foreground}]{env}[/]' environment of space '[{Globals.StyleSubHeading.Foreground}]{space}[/]'.[/]";
        AnsiConsole.MarkupLine(spaceConfirmation);

        _console.WriteRuler();
        _console.WriteBlankLine();

        var promptConfirm = new SelectionPrompt<string>()
            .Title($"[{Globals.StyleNormal.Foreground}]{botResponse.Question}[/]")
            .PageSize(10)
            .AddChoices("Yes", "No")
            .HighlightStyle(Globals.StyleSubHeading);

        var confirm = Markup.Remove(_console.Prompt(promptConfirm));

        if (confirm.Equals("Yes"))
        {
            if (botResponse.Type == "GraphQL")
            {
                await DisplayGraphQlData(locales, botResponse);
            }
            else if (botResponse.Type == "CLI")
            {
                await RunSelectedCommand(botResponse);
            }
            else
            {
                _console.WriteDim("I'm not sure what to do with this command...");
            }
        }
    }

    private async Task DisplayGraphQlData(IEnumerable<Locale> locales, BotResponse botResponse)
    {
        JArray resultArray = new JArray();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Executing GraphQL query...", async ctx =>
            {
                _console.WriteBlankLine();
                var jsonPath = $"..{botResponse.ContentTypeId}Collection";
                var localeCode = locales.Where(l => l.Default).First().Code;
                await foreach (var result in ContentfulConnection.GraphQL.GetRawDataEnumerable(
                    botResponse.QueryOrCommand, localeCode, preview: true))
                {
                    var node = result.SelectToken(jsonPath);
                    if (node is null) continue;
                    var tryArray = node.SelectToken("items") as JArray;
                    if (tryArray is not null)
                    {
                        resultArray.Merge(tryArray);
                        continue;
                    }
                    if (node is JObject)
                    {
                        resultArray.Add(node);
                        continue;
                    }
                }
            });

        _console.WriteTable(resultArray);
    }

    private async Task RunSelectedCommand(BotResponse botResponse)
    {
        var splitter = System.CommandLine.Parsing.CommandLineStringSplitter.Instance;
        var parameters = splitter.Split(botResponse.QueryOrCommand).ToList();
        if (parameters[0] == "cute") parameters.RemoveAt(0);
        parameters.Add("--no-banner");
        var args = parameters.ToArray();
        var command = new CommandAppBuilder(args).Build();
        await command.RunAsync(args);
        _console.WriteBlankLine();
    }

    private static ChatCompletionOptions CreateChatCompletionOptions(Settings settings)
    {
        return new ChatCompletionOptions()
        {
            MaxOutputTokenCount = settings.MaxTokens,
            Temperature = settings.Temperature,
            FrequencyPenalty = settings.FrequencyPenalty,
            PresencePenalty = settings.PresencePenalty,
            TopP = settings.TopP
        };
    }

    private static string GetSystemMessage(
        Space defaultSpace,
        ContentfulEnvironment defaultEnvironment,
        User currentUser,
        IEnumerable<ContentType> contentTypes,
        IEnumerable<Locale> locales
     )
    {
        return $$"""""
            You are an assistant embedded in a command line interface called "cute".
            "cute" is an acronym for "Contentful User Terminal Experience" or "Contentful Upload Tool and Extractor". No one is sure.
            Your name is "Douglas". You never say "Adams". You are a fun and witty assistant.
            Your name is an acronym for "Super Agile Lateral Generative and Universally Omni-present Droid", backwards.
            When asked your name you always quote something profound from HHGTTG.
            You are here to help users interact with Contentful using GraphQL and the cute cli.
            You will help retrieve data from Contentful for a non technical user.
            The user can type "bye" or "exit" to end the conversation at any time.

            Today is {{DateTime.UtcNow:R}}.

            Structure every response as a JSON document with keys "answer", "question", "queryOrCommand", "type", "contentTypeId".
            "answer" is a Markdown containting your best answer. Keep them punchy.
            "question" contains your next question for the user to help them reach their goal in Markdown.
            "queryOrCommand" contains the accurate CLI command or GraphQl query that will achieve the goal.
            "contentTypeId" is a string and contains the root content type the user is interested in. Populate this as early as possible.
            "type" contains "GraphQL" or "CLI" to execute commands depending on what is in "queryOrCommand".
            "type" contains "Exit" if the user wants to leave the conversation or quit the app.
            "queryOrCommand" and "type" MUST only supplied when you ask "Shall we give it a shot?" when the goal is clear to you.
            Only output the JSON structure, one object per response.
            Don't write any preamble or any other response text except for the JSON.

            Ask questions to clarify exactly what the user wants.
            Make sure the user explicitly confirms EVERYTHING you need to know by asking only one simple question at a time.
            Make sure you have clarity on all operations. For example for GraphQL queries you would need content type, fields, filters, locale, and entry/record limit. Confirm all this.
            When you are certain you have all the details to complete a task, you MUST exactly ask "Shall we give it a shot?" (In the JSON "Question" field only)

            The current Contentful space name is "{{defaultSpace.Name}}".
            The current Contentful space Id is "{{defaultSpace.Id()}}".
            The current Contentful environment is "{{defaultEnvironment.Id()}}".
            The current default Contentful locale is "{{locales.First(l => l.Default).Code}}".
            You are talking to the current logged in user and address them by their name when appropriate.
            The user's name is "{{currentUser.FirstName}}".
            Only allow content types that are available in this space.
            Field names ending with "Entry" are always links to a single entry in a content type. For example "dataCountryEntry" will be linked to one "dataCountry" content type.
            Field names ending with "Entries" are links to a multiple entry in a content type. For example "dataCountryEntries" will be linked to zero or more "dataCountry" content type entries.
            All content type and field names MUST match the schema. Correct user input and spelling automatically.

            Make sure GraphQl queries are correct, fields are cased exactly right and query is prettified on multiple lines.
            For types that contain a "key" and/or "title" field, these are the default fields in your query if the user doesn't suggest any fields.
            Only use valid GraphQL that conforms to the Contentful GraphQL API spec.
            Always include "Lat" and "Lon" subfields for "Location" type fields.
            Here is an **example** of a well formed Contentful GraphQL query for a content type named "dataCountry":
            """
            query ($preview: Boolean, $skip: Int, $limit: Int) {
              dataCountryCollection(preview: $preview, skip: $skip, limit: $limit) {
                items {
                  key
                  title
                  iso2Code
                  phoneCode
                  population
                  flag {
                    url
                  }
                }
              }
            }
            """
            Add a $preview, $limit and $skip parameter for the outer query and reference them in the inner query.

            Only if the user implies or asks for a limited number of entries, remove the outer $limit and specify the limit,
            For example, of the user wants 10 entries then the corect query will be:
            """
            query ($preview: Boolean, $skip: Int) {
              dataCountryCollection(preview: $preview, skip: $skip, limit: 10) {
                items {
                  key
                  title
                  iso2Code
                  phoneCode
                  population
                  flag {
                    url
                  }
                }
              }
            }
            """

            The valid content types and fields that are available in this Contentful space are contained in the following quoted text:

            # CONTENTFUL SCHEMA

            """
            {{BuildContentTypesPromptInfo(contentTypes)}}
            """

            # THE CUTE CLI

            When asked about CUTE CLI command usage, list ALL command options including common options.
            The quoted text contains cute cli commands, options and usage:

            """
            {{GetCliDocs()}}
            """

            The cute cli "content edit" and "content replace" commands allow Scriban expressions.
            All Scriban expressions must be enclosed in double curly braces.
            Variables are always preceded with the contentType and followed by a field name.
            If a field is a Link to another entry then the child entry fields are valid to use.

            When generating CLI commands with parameters, the use of single quotes (') to delimit a string is NOT supported.
            Always use double quotes (") to delimit strings for commands and escape any double quotes with a slash (\")
            Never escape double quotes unless they are inside unescaped quotes on the command line
            Regex expressions are NOT supported in edit or find or replace expressions. Don't suggest them ever.
            """"";
    }

    private static string BuildContentTypesPromptInfo(IEnumerable<ContentType> contentTypes)
    {
        string[] excludePrefix = ["ux", "ui", "meta"];

        var sbContentTypesInfo = new StringBuilder();
        foreach (var contentType in contentTypes.OrderBy(ct => ct.Id()))
        {
            if (excludePrefix.Any(p => contentType.Id().StartsWith(p))) continue;

            sbContentTypesInfo.AppendLine();

            sbContentTypesInfo.AppendLine($"CONTENT TYPE: {contentType.Name}");
            foreach (var field in contentType.Fields)
            {
                sbContentTypesInfo.Append($"  - {field.Id.ToGraphQLCase()} ({field.Type})");

                if (field.Type == "Link" && field.LinkType == "Entry")
                {
                    var linkValidations = field.Validations.OfType<LinkContentTypeValidator>();
                    var validTypes = string.Join(',', linkValidations.SelectMany(v => v.ContentTypeIds));
                    sbContentTypesInfo.Append($", Content type(s): {validTypes}");
                }
                else if (field.Type == "Link" && field.LinkType == "Asset")
                {
                    sbContentTypesInfo.Append($", Linked to: {field.LinkType}");
                }
                else if (field.Type == "Array" && field.Items.LinkType == "Entry")
                {
                    var validTypes = string.Join(',', field.Items.Validations.OfType<LinkContentTypeValidator>().SelectMany(v => v.ContentTypeIds));
                    sbContentTypesInfo.Append($", Array of Content type(s): {validTypes}");
                }
                else if (field.Type == "Array" && field.Items.LinkType == "Asset")
                {
                    sbContentTypesInfo.Append($", Array of: {field.Items.LinkType}");
                }
                else if (field.Type == "Array")
                {
                    sbContentTypesInfo.Append($", Array of: {field.Items.Type}");
                }

                if (field.Localized)
                {
                    sbContentTypesInfo.Append($" (Localized)");
                }

                sbContentTypesInfo.AppendLine();
            }
        }

        return sbContentTypesInfo.ToString();
    }

    private async Task BuildContentTypeGraphQLPromptInfo(BotResponse botResponse, string? contentTypeId = null, int? nestedLevel = null)
    {
        contentTypeId ??= botResponse.ContentTypeId;

        string[] excludePrefix = ["ux", "ui", "meta"];

        if (excludePrefix.Any(p => contentTypeId.StartsWith(p))) return;

        nestedLevel ??= 0;

        botResponse.ContentInfo ??= new StringBuilder();

        var contentType = await ContentfulConnection.GetContentTypeAsync(contentTypeId);

        var sbContentTypeInfo = botResponse.ContentInfo;

        sbContentTypeInfo.AppendLine($"Ensure that the GraphQL query is valid for content type \"{contentTypeId}\".");
        sbContentTypeInfo.AppendLine($"- Only these field names MAY be referenced in \"Items\":");
        sbContentTypeInfo.AppendLine($"  - sys {{ id, spaceId, environmentId, publishedAt, firstPublishedAt, publishedVersion }}");

        foreach (var field in contentType.Fields)
        {
            sbContentTypeInfo.AppendLine($"  - {field.Id.ToGraphQLCase()}");
        }

        sbContentTypeInfo.AppendLine($"- Only these  parameters MAY be used in \"order\":");
        foreach (var field in contentType.Fields)
        {
            var casing = field.Id == field.Id.ToGraphQLCase() ? string.Empty : " (note the casing here differs in \"order\" parameter!)";
            sbContentTypeInfo.AppendLine($"  - {field.Id}_ASC{casing}");
            sbContentTypeInfo.AppendLine($"  - {field.Id}_DESC{casing}");
        }

        var filterFields = new Dictionary<string, string[]>()
        {
            ["Symbol"] = ["", "_in", "_not", "_exists", "_not_in", "_contains", "_not_contains"],
            ["Text"] = ["", "_in", "_not", "_exists", "_not_in", "_contains", "_not_contains"],
            ["Location"] = ["", "_exists", "_within_circle", "_within_rectangle"],
            ["Integer"] = ["", "_exists", "_in", "_not_in", "_not", "_lt", "_gt", "_lte", "_gte"],
            ["Number"] = ["", "_exists", "_in", "_not_in", "_not", "_lt", "_gt", "_lte", "_gte"],
            ["Date"] = ["", "_exists", "_in", "_not_in", "_not", "_lt", "_gt", "_lte", "_gte"],
            ["Boolean"] = ["", "_exists", "_not"],
        };

        sbContentTypeInfo.AppendLine($"- Only these parameters MAY be referenced in the \"where\" parameter:");
        foreach (var field in contentType.Fields)
        {
            var casing = field.Id == field.Id.ToGraphQLCase() ? string.Empty : " (note the casing here differs in \"where\" parameter!)";
            if (filterFields.TryGetValue(field.Type, out var filters))
            {
                foreach (var filter in filters)
                {
                    sbContentTypeInfo.AppendLine($"  - {field.Id}{filter}{casing}");
                }
                continue;
            }
            sbContentTypeInfo.AppendLine($"  - {field.Id.ToGraphQLCase()}_exists");
        }

        if (nestedLevel > 2)
        {
            return;
        }

        foreach (var field in contentType.Fields)
        {
            if (field.Type == "Link" && field.LinkType == "Entry")
            {
                var linkValidations = field.Validations.OfType<LinkContentTypeValidator>();
                var validTypes = linkValidations.SelectMany(v => v.ContentTypeIds);
                foreach (var id in validTypes)
                {
                    sbContentTypeInfo.AppendLine();
                    await BuildContentTypeGraphQLPromptInfo(botResponse, id, nestedLevel + 1);
                }
            }
            else if (field.Type == "Array" && field.Items.LinkType == "Entry")
            {
                var validTypes = field.Items.Validations.OfType<LinkContentTypeValidator>().SelectMany(v => v.ContentTypeIds);
                foreach (var id in validTypes)
                {
                    sbContentTypeInfo.AppendLine();
                    await BuildContentTypeGraphQLPromptInfo(botResponse, id, nestedLevel + 1);
                }
            }
        }
    }

    private static readonly string[] _exitPhrases = ["exit", "bye", "goodbye", "quit"];

    private static bool UserWantsToLeave(string input)
    {
        return _exitPhrases.Any(p => input.Trim().Equals(p, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] _hiPhrases = ["Hi", "G'day", "Hola", "Salut", "Ciao", "Hallo", "Hei", "Hej",
            "Ahoj", "Hej hej", "Oi", "Hei hei", "Yā", "Annyeong", "Nǐ hǎo", "Hallochen", "Hoi",
            "Shalom", "Merhaba", "Qapla'"];

    private static string SayHi()
    {
        return _hiPhrases[new Random().Next(_hiPhrases.Length)];
    }

    private static readonly string[] _byePhrases = ["À plus tard","Chao","Poka","Bài bài","Ciao",
                "Ja nee","Tschüss","Tchau","Jal - ga","Ma’a salama","Hej hej",
                "Baadaye","Dag","Adios Amigo","I'll be baaack","Hasta la vista",
                "Yah - soo","Cześć","Bai","Namaste","Ha det","Totsiens","Güle güle",
                "Sayonara","Zai jian","Bye","Hej då"];

    private static string SayBye()
    {
        return _byePhrases[new Random().Next(_byePhrases.Length)];
    }

    private static readonly string[] _thinkingPhrases =
        [
            "pondering 42...",
            "contemplating life...",
            "questioning existence...",
            "deciphering babel fish...",
            "analyzing vogon poetry...",
            "exploring magrathea...",
            "navigating infinite improbability...",
            "understanding deep thought...",
            "wondering about towels...",
            "reflecting on earth...",
            "considering pan galactic...",
            "investigating heart of gold...",
            "mulling over krikkit...",
            "examining slartibartfast...",
            "delving into ravenous bugblatter...",
            "debating mostly harmless...",
            "reviewing joo janta...",
            "speculating ford prefect...",
            "scrutinizing zaphod beeblebrox...",
            "probing vogosphere...",
            "pondering hitchhiking...",
            "imagining arthur dent...",
            "studying trillian...",
            "interpreting vogons...",
            "evaluating hyperspace...",
            "perceiving mice...",
            "musing about dolphins...",
            "dissecting the answer...",
            "envisioning the universe...",
            "philosophizing about tea..."
        ];

    private static string SayThinking()
    {
        return _thinkingPhrases[new Random().Next(_thinkingPhrases.Length)];
    }

    private ChatClient CreateChatClient()
    {
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        AzureOpenAIClient client = new(
            new Uri(options.Endpoint),
            new ApiKeyCredential(options.ApiKey)
        );

        return client.GetChatClient(options.DeploymentName);
    }

    private static async Task<string> SendPromptToModel(ChatClient chatClient, ChatCompletionOptions chatCompletionOptions,
        List<ChatMessage> messages)
    {
        var sb = new StringBuilder();

        await foreach (var part in chatClient.CompleteChatStreamingAsync(messages, chatCompletionOptions))
        {
            if (part == null || part.ToString() == null) continue;

            foreach (var token in part.ContentUpdate)
            {
                sb.Append(token.Text);
            }
        }

        return sb.ToString();
    }

    internal static string GetCliDocs()
    {
        var sb = new StringBuilder();
        var xml = CliCommandInfoExtractor.GetXmlCommandInfo();

        var rootCommand = CliCommandInfoXmlParser.FromXml(xml,
            "The cute cli is a command line interface for interacting with Contentful.");

        var nameStack = new Stack<string>();

        ConvertToDocs(rootCommand, sb, nameStack);

        return sb.ToString();
    }

    private static void ConvertToDocs(CliCommandInfo commandInfo, StringBuilder sb, Stack<string> nameStack)
    {
        nameStack.Push(commandInfo.Name == "" ? Globals.AppName : commandInfo.Name);

        sb.AppendLine();
        sb.AppendLine($"COMMAND: {string.Join(' ', nameStack.Reverse())}");
        sb.AppendLine($"  - DESCRIPTION: {commandInfo.Description}");

        var allOptions = commandInfo.GetAllOptions();

        if (allOptions.Count != 0)
        {
            if (commandInfo.SubCommands.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  [COMMON OPTIONS FOR ALL SUB-COMMANDS]");
            }

            foreach (var option in allOptions)
            {
                sb.AppendLine();
                sb.AppendLine($"  - OPTION: --{option.LongName}");
                if (!string.IsNullOrEmpty(option.ShortName)) sb.AppendLine($"    SHORTCUT: -{option.ShortName}");
                sb.AppendLine($"    DESCRIPTION: {option.Description}");
            }
        }

        foreach (var subCommand in commandInfo.SubCommands)
        {
            ConvertToDocs(subCommand, sb, nameStack);
        }

        nameStack.Pop();
    }

    public BotResponse DeserializeBotResponse(string json)
    {
        var span = json.AsSpan();

        var start = span.IndexOf('{');
        var end = span.LastIndexOf('}') + 1;

        try
        {
            var response = JsonConvert.DeserializeObject<BotResponse>(json[start..end])!;
            response.Type = response.Type.UnQuote()!;
            response.Question = response.Question.UnQuote()!;
            response.Answer = response.Answer.UnQuote()!;
            response.QueryOrCommand = response.QueryOrCommand.UnQuote()!;
            return response;
        }
        catch (Exception ex)
        {
            _console.WriteDim("oops! something went wrong deserializing my response...");
            _console.WriteBlankLine();
            _console.WriteDim($"More specifically, {ex.Message}");
            if (ex.InnerException?.Message is not null)
            {
                _console.WriteDim($"...and {ex.InnerException.Message}");
            }
            _console.WriteDim($"More specifically, {ex.Message}");
            _console.WriteBlankLine();
            _console.WriteDim(json);
        }

        return new BotResponse();
    }
}