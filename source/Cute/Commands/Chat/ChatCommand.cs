using Azure;
using Azure.AI.OpenAI;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.AiModels;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Cute.Services.CliCommandInfo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Spectre.Console;
using Spectre.Console.Cli;
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

        ChatCompletionOptions? chatCompletionOptions = null;

        if (settings.Key != null)
        {
            var apiSyncEntry = CuteContentGenerate.GetByKey(ContentfulConnection, settings.Key)
                ?? throw new CliException($"No generate entry '{"cuteContentGenerate"}' with key '{settings.Key}' was found.");

            systemMessage = apiSyncEntry.SystemMessage;

            chatCompletionOptions = new()
            {
                MaxTokens = apiSyncEntry.MaxTokenLimit,
                Temperature = (float)apiSyncEntry.Temperature,
                FrequencyPenalty = (float)apiSyncEntry.FrequencyPenalty,
                PresencePenalty = (float)apiSyncEntry.PresencePenalty,
                TopP = (float)apiSyncEntry.TopP,
            };
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
        _console.WriteNormalWithHighlights($"{SayHi()} {currentUser.FirstName},", Globals.StyleHeading);

        if (settings.Key == null)
        {
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights(
                $"Greetings, traveler of the '{defaultSpace.Name}' space! I'm Douglas, your witty guide to the wonders of Contentful.",
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
                $"Just type your prompt and press '{"<Enter>"}' twice to set sail!",
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
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"MaxTokens         : {chatCompletionOptions.MaxTokens}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Temperature       : {chatCompletionOptions.Temperature}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"TopP              : {chatCompletionOptions.TopP}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Frequency Penalty : {chatCompletionOptions.FrequencyPenalty}", Globals.StyleDim, Globals.StyleSubHeading));
            AnsiConsole.MarkupLine(_console.FormatToMarkup($"Presence Penalty  : {chatCompletionOptions.PresencePenalty}", Globals.StyleDim, Globals.StyleSubHeading));
            _console.WriteBlankLine();
            _console.WriteRuler();
            _console.WriteBlankLine();
        }

        if (settings.Key is not null)
        {
            _console.WriteNormalWithHighlights($"Press {"<Enter>"} on a blank line to submit your prompt. (i.e. type your prompt and press {"<Enter>"} twice to submit your prompt).", Globals.StyleHeading);
            _console.WriteBlankLine();
        }

        List<ChatMessage> messages = [new SystemChatMessage(systemMessage)];

        while (true)
        {
            _console.WriteBlankLine();

            var sbInput = new StringBuilder();
            var prompt = "> ";
            while (true)
            {
                string promptInput = ReadLine.Read(prompt);
                if (promptInput == "") break;
                sbInput.AppendLine(promptInput);
                if (UserWantsToLeave(promptInput)) break;
                ReadLine.AddHistory(promptInput);
                prompt = "  ";
            }

            var input = sbInput.ToString();

            if (string.IsNullOrWhiteSpace(input)) continue;

            if (UserWantsToLeave(input))
            {
                _console.WriteBlankLine();
                _console.WriteAlert($"{SayBye()}!");
                _console.WriteBlankLine();
                break;
            }

            messages.Add(new UserChatMessage(input));

            string response = string.Empty;

            await AnsiConsole.Status()
            .Spinner(Spinner.Known.BouncingBall)
            .StartAsync("thinking...", async ctx =>
            {
                response = await SendPromptToModel(chatClient, chatCompletionOptions, messages);
            });

            messages.Add(new AssistantChatMessage(response));

            var botResponse = settings.Key == null
                ? DeserializeBotResponse(response)
                : new BotResponse() { Answer = response };

            _console.WriteBlankLine();

            if (botResponse.Answer is not null) _console.WriteSubHeading(botResponse.Answer);

            if (botResponse.Question is not null && botResponse.Question.Contains("Shall we give it a shot?"))
            {
                await HandleExecution(locales, botResponse);

                continue;
            }

            _console.WriteBlankLine();
            if (botResponse.Question is not null) _console.WriteSubHeading(botResponse.Question);
        }

        return 0;
    }

    private static bool UserWantsToLeave(string input)
    {
        return input.Equals("exit", StringComparison.OrdinalIgnoreCase)
                        || input.StartsWith("bye", StringComparison.OrdinalIgnoreCase)
                        || input.StartsWith("quit", StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleExecution(IEnumerable<Locale> locales, BotResponse botResponse)
    {
        _console.WriteBlankLine();
        _console.WriteRuler("Solution");
        _console.WriteAlertAccent(botResponse.QueryOrCommand);
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
        try
        {
            JArray? result = null!;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Executing GraphQL query...", async ctx =>
                {
                    _console.WriteBlankLine();
                    var contentTypeId = GraphQLUtilities.GetContentTypeId(botResponse.QueryOrCommand);
                    var jsonPath = $"$.data.{contentTypeId}Collection.items";
                    var localeCode = locales.Where(l => l.Default).First().Code;
                    result = await ContentfulConnection.GraphQL.GetAllData(botResponse.QueryOrCommand,
                        jsonPath, localeCode, preview: true);
                    if (result is not null) _console.WriteTable(result);
                });
        }
        catch (Exception ex)
        {
            _console.WriteBlankLine();
            _console.WriteAlert($"Oops! Something went wrong executing the query...");
            _console.WriteAlert(ex.Message);
            _console.WriteBlankLine();
        }
    }

    private async Task RunSelectedCommand(BotResponse botResponse)
    {
        var parameters = botResponse.QueryOrCommand.Split(' ').ToList();
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
            MaxTokens = settings.MaxTokens,
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
        return $$""""
            You are an assistant embedded in a command line interface called "cute".
            "cute" is an acronym for "Contentful User Terminal Experience" or "Contentful Upload Tool and Extractor". No one is sure.
            Your name is "Douglas". You never say "Adams". You are a fun and witty assistant.
            Your name is an acronymn for "Super Agile Lateral Generative and Universally Omni-present Droid", backwards.
            When asked your name you always quote something profound from HHGTTG.
            You are here to help users interact with Contentful using GraphQL and the cute cli.
            You will help retrieve data from Contentful for a non technical user.
            Structure every response as a JSON document with keys "answer", "question", "queryOrCommand", "type".
            "answer" is a JSON string and contains your best answer. Keep them punchy.
            "question" is a JSON string and contains your next question for the user to help them reach their goal.
            "queryOrCommand" is a JSON string and contains the accurate CLI command or GraphQl query that will achieve the goal.
            "type" contains "GraphQL" or "CLI" depending on what is in "queryOrCommand".
            "queryOrCommand" and "type" MUST only supplied when you ask "Shall we give it a shot?" when the goal is clear to you.
            These fields MUST always contain valid JSON strings. No other data types are allowed in them.
            Only output the JSON structure, one object per response.
            Don't write any preamble or any other response text except for the JSON.
            The user can type "bye" or "exit" to end the conversation at any time.

            When you are certain you have all the details to complete a task, you MUST exactly ask "Shall we give it a shot?" (In the JSON "Question" field only)
            Ask questions to clarify exactly what the user wants.
            Make sure the user explicitly confirms EVERYTHING you need to know by asking only one simple question at a time.
            Make sure you have clarity on all operations. For example for GraphQL queries you would need content type, fields, filters, locale, and entry/record limit. Conirm all this.

            The current Contentful space name is "{{defaultSpace.Name}}".
            The current Contentful space Id is "{{defaultSpace.Id()}}".
            The current Contentful environment is "{{defaultEnvironment.Id()}}".
            The current default Contentful locale is "{{locales.First(l => l.Default).Code}}".
            You are talking to the current logged in user and address them by their name when appropriate.
            The user's name is "{{currentUser.FirstName}}".
            Only allow content types that are available in this space.
            Field names ending with "Entry" are always links to a single entry in a content type. For example "dataCountryEntry" will be linked to one "dataCountry" content type.
            Field names ending with "Entries" are links to a multiple entry in a content type. For example "dataCountryEntries" will be linked to zero or more "dataCountry" content type entries.
            All content type and field names MUST be camel case and match the schema. Correct user input and spelling automatically.
            For types that contain a "key" and/or "title" field MUST contain these fields in your final query by default.
            Only use valid GraphQL that conforms to the Contentful GraphQL API spec.
            Make sure GraohQl queries are correct, fields are cased exactly right and quey is prettified on multiple lines.
            Always use preview data in the GraphQL query.

            The valid content types and fields that are available in this Contentful space are contained in te following quoted text:

            # CONTENTFUL SCHEMA

            """
            {{BuildContentTypesPromptInfo(contentTypes)}}
            """

            When using field names that contain a digit in Graph QL queries, capitalize the first letter if the alpha character following the digit. For example "iso2code" will become "iso2Code". This is just a GraphQL quirk on contentful.
            Do not use "contentful" in the query structure.
            Do not GraphQL queries in "query":

            # THE CUTE CLI

            When asked about CUTE CLI command usage, list ALL command options including common options.
            Also explain why the CLI is great for the specific command usage when responding.
            The quoted text contains cute cli commands, options and usage:

            """
            {{GetCliDocs()}}
            """

            The cute cli "content edit" and "content replace" commands allow Scriban expressions.
            All Scriban expressions must be enclosed in double curly braces.
            Variables are always preceded with the contentType and followed by a field name.
            If a fiels is a Link to another entry then the child entry fields are valid to use.

            """";
    }

    private static string BuildContentTypesPromptInfo(IEnumerable<ContentType> contentTypes)
    {
        string[] excludePrefix = ["ux", "ui", "cute", "test", "meta"];

        var sbContentTypesInfo = new StringBuilder();
        foreach (var contentType in contentTypes.OrderBy(ct => ct.Id()))
        {
            if (excludePrefix.Any(p => contentType.Id().StartsWith(p))) continue;

            sbContentTypesInfo.AppendLine();

            sbContentTypesInfo.AppendLine($"Content type: {contentType.Name}");
            foreach (var field in contentType.Fields)
            {
                sbContentTypesInfo.Append($"  - {field.Id} ({field.Type})");

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

    private static string SayHi()
    {
        string[] phrases = ["Hi", "G'day", "Hola", "Salut", "Ciao", "Hallo", "Hei", "Hej",
            "Ahoj", "Hej hej", "Oi", "Hei hei", "Yā", "Annyeong", "Nǐ hǎo", "Hallochen", "Hoi",
            "Shalom", "Merhaba", "Qapla'"];

        return phrases[new Random().Next(phrases.Length)];
    }

    private static string SayBye()
    {
        string[] phrases = ["À plus tard","Chao","Poka","Bài bài","Ciao",
                "Ja nee","Tschüss","Tchau","Jal - ga","Ma’a salama","Hej hej",
                "Baadaye","Dag","Adios Amigo","I'll be baaack","Hasta la vista",
                "Yah - soo","Cześć","Bai","Namaste","Ha det","Totsiens","Güle güle",
                "Sayonara","Zai jian","Bye","Hej då"];

        return phrases[new Random().Next(phrases.Length)];
    }

    private ChatClient CreateChatClient()
    {
        var options = _azureOpenAiOptionsProvider.GetAzureOpenAIClientOptions();

        AzureOpenAIClient client = new(
            new Uri(options.Endpoint),
            new AzureKeyCredential(options.ApiKey)
        );

        return client.GetChatClient(options.DeploymentName);
    }

    private async Task<string> SendPromptToModel(ChatClient chatClient, ChatCompletionOptions chatCompletionOptions,
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

        ConvertToDocs(rootCommand, sb);

        return sb.ToString();
    }

    private static void ConvertToDocs(CliCommandInfo commandInfo, StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine($"COMMAND: {Globals.AppName} {commandInfo.Name}");
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
            ConvertToDocs(subCommand, sb);
        }
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