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
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Extensions;
using Cute.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Cute.Commands.Chat;

public sealed class ChatCommand(IConsoleWriter console, ILogger<ChatCommand> logger,
    AppSettings appSettings, IAzureOpenAiOptionsProvider azureOpenAiOptionsProvider)
    : BaseLoggedInCommand<ChatCommand.Settings>(console, logger, appSettings)
{
    private readonly IAzureOpenAiOptionsProvider _azureOpenAiOptionsProvider = azureOpenAiOptionsProvider;

    public class Settings : LoggedInSettings
    {
    }

    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
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
        _console.WriteNormalWithHighlights($"Howdy {currentUser.FirstName},", Globals.StyleHeading);

        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"I can provide you with info on the types and fields in the '{defaultSpace.Name}' space, or", Globals.StyleHeading);
        _console.WriteNormalWithHighlights($"assist you with writing GraphQL queries, cli commands, etc.", Globals.StyleHeading);
        _console.WriteBlankLine();
        _console.WriteNormalWithHighlights($"Type '{"bye"}' or '{"exit"}' to end our chat.", Globals.StyleHeading);
        _console.WriteBlankLine();

        var chatClient = CreateChatClient();

        var chatCompletionOptions = CreateCompletionOptions();

        var systemMessage = GetSystemMessage(defaultSpace, defaultEnvironment, currentUser, contentTypes, locales);

        List<ChatMessage> messages = [new SystemChatMessage(systemMessage)];

        while (true)
        {
            _console.WriteBlankLine();

            string input = AnsiConsole.Prompt(
                new TextPrompt<string>($"[{Globals.StyleNormal.Foreground}]> [/]")
                    .PromptStyle(Globals.StyleHeading)
                    .AllowEmpty());

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || input.StartsWith("bye", StringComparison.OrdinalIgnoreCase))
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

            var botResponse = DeserializeBotResponse(response);

            _console.WriteBlankLine();
            if (botResponse.Answer is not null) _console.WriteSubHeading(botResponse.Answer);

            if (botResponse.Question is not null && botResponse.Question.Contains("Shall we give it a shot?"))
            {
                _console.WriteBlankLine();
                _console.WriteHeading($"Solution: ({botResponse.Type})");
                _console.WriteBlankLine();
                _console.WriteAlertAccent(botResponse.QueryOrCommand);

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

                continue;
            }

            _console.WriteBlankLine();
            if (botResponse.Question is not null) _console.WriteSubHeading(botResponse.Question);
        }

        return 0;
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

    private static ChatCompletionOptions CreateCompletionOptions()
    {
        return new ChatCompletionOptions()
        {
            MaxTokens = 1500,
            Temperature = 0.2f,
            FrequencyPenalty = 0.1f,
            PresencePenalty = 0.1f,
            TopP = (float)0.85,
        };
    }

    private string GetSystemMessage(
        Space defaultSpace,
        ContentfulEnvironment defaultEnvironment,
        User currentUser,
        IEnumerable<ContentType> contentTypes,
        IEnumerable<Locale> locales
     )
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

        return $$"""
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

            The valid content types and fields that are available in this Contentful space are:

            # CONTENTFUL SCHEMA

            {{sbContentTypesInfo}}

            The cute cli content edit and find and replace commands allow Scriban expressions.
            All Scriban expressions must be enclosed in double curly braces.
            Variables are always preceded with the contentType and followed by a field name.
            If a fiels is a Link to another entry then the child entry fields are valid to use.

            # THE CUTE CLI

            When asked about CUTE CLI command usage, list ALL command options including common options.
            Also explain why the CLI is great for the specific command usage when responding.
            The cute cli supports the following commands:

            {{GetCliDocs()}}

            """;
    }

    private string SayBye()
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

    internal class CliCommandInfo
    {
        public string Name { get; set; } = default!;

        public string Description { get; set; } = default!;

        public List<CliCommandInfo> SubCommands { get; set; } = new();

        public List<CliOptionInfo> Options { get; set; } = new();
        public Dictionary<string, int> SubOptionCount { get; set; } = new();

        public CliCommandInfo? Parent = null;

        public int Descendants = 0;
    }

    internal class CliOptionInfo
    {
        public string ShortName { get; set; } = default!;

        public string LongName { get; set; } = default!;

        public string Description { get; set; } = default!;
    }

    internal static string GetCliDocs()
    {
        using var xmlWriter = new StringWriter();

        var console = new StringWriterConsole(xmlWriter);

        var app = new CommandAppBuilder([]).Build(config => config.Settings.Console = console);

        var exitCode = app.Run(["cli", "xmldoc"]);

        var xmlOutput = xmlWriter.ToString();

        XDocument xmlDoc = XDocument.Parse(xmlOutput);

        var commandInfos = new CliCommandInfo()
        {
            Name = string.Empty,
            Description = "The cute cli is a command line interface for interacting with Contentful.",
        };

        ProcessCliXml(xmlDoc, commandInfos);

        ExtractCommonOptions(commandInfos);

        PruneCommonOptions(commandInfos);

        var sb = new StringBuilder();

        ConvertToDocs(commandInfos, sb);

        var ret = sb.ToString();

        return ret;
    }

    private static void ProcessCliXml(XDocument xmlDoc, CliCommandInfo commandInfos)
    {
        var rootCommands = xmlDoc.Element("Model")?.Elements("Command");
        if (rootCommands != null)
        {
            foreach (var command in rootCommands)
            {
                ProcessCommand(command, "", 0, commandInfos);
            }
        }
        else
        {
            throw new Exception("No commands found in the XML.");
        }
    }

    private static void ExtractCommonOptions(CliCommandInfo currentCommand, CliCommandInfo? parent = null)
    {
        if (currentCommand.SubCommands.Any())
        {
            foreach (var subCommand in currentCommand.SubCommands)
            {
                ExtractCommonOptions(subCommand, currentCommand);
            }
        }

        var currentParent = currentCommand.Parent;

        while (currentParent is not null)
        {
            if (currentCommand.Options.Count > 0) currentParent.Descendants++;

            foreach (var option in currentCommand.Options)
            {
                if (!currentParent.SubOptionCount.ContainsKey(option.LongName))
                {
                    currentParent.SubOptionCount.Add(option.LongName, 0);
                    currentParent.Options.Add(option);
                }
                currentParent.SubOptionCount[option.LongName]++;
            }
            currentParent = currentParent.Parent;
        }
    }

    private static void PruneCommonOptions(CliCommandInfo currentCommand)
    {
        if (currentCommand.SubCommands.Any())
        {
            var removeOptions = currentCommand.SubOptionCount
                .Where(kvp => kvp.Value < currentCommand.Descendants)
                .Select(kvp => kvp.Key)
                .ToList();

            currentCommand.Options.RemoveAll(o => removeOptions.Contains(o.LongName));

            foreach (var subCommand in currentCommand.SubCommands)
            {
                PruneCommonOptions(subCommand);
            }
        }

        var currentParent = currentCommand.Parent;

        while (currentParent is not null)
        {
            currentCommand.Options.RemoveAll(o => currentParent.Options.Where(p => o.LongName == p.LongName).Any());

            currentParent = currentParent.Parent;
        }
        currentCommand.Parent = null!;
        currentCommand.SubOptionCount = null!;
    }

    private static void ConvertToDocs(CliCommandInfo commandInfo, StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine($"COMMAND: {Globals.AppName} {commandInfo.Name}");
        sb.AppendLine($"  - DESCRIPTION: {commandInfo.Description}");

        if (commandInfo.Options.Count != 0)
        {
            if (commandInfo.SubCommands.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  [COMMON OPTIONS FOR ALL SUB-COMMANDS]");
            }

            foreach (var option in commandInfo.Options)
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

    private static void ProcessCommand(XElement commandNode,
        string commandPath, int level, CliCommandInfo parentCommandInfo)
    {
        var currentCommandName = commandNode.Attribute("Name")?.Value ?? "";

        var fullCommandPath = string.IsNullOrEmpty(commandPath) ? currentCommandName : $"{commandPath} {currentCommandName}";

        var headingLevel = new string('#', level + 2);

        var commandDescription = GetDescriptionText(commandNode.Element("Description"));

        var commandInfo = new CliCommandInfo
        {
            Name = fullCommandPath,
        };

        parentCommandInfo.SubCommands.Add(commandInfo);

        commandInfo.Parent = parentCommandInfo;

        if (!string.IsNullOrEmpty(commandDescription))
        {
            commandInfo.Description = commandDescription;
        }

        var parametersNode = commandNode.Element("Parameters");

        if (parametersNode != null && parametersNode.Elements("Option").Any())
        {
            foreach (var param in parametersNode.Elements("Option"))
            {
                var shortName = param.Attribute("Short")?.Value;
                var longName = param.Attribute("Long")?.Value;
                var value = param.Attribute("Value")?.Value;
                var paramDescription = GetDescriptionText(param.Element("Description"));

                // Escape pipes and line breaks in descriptions
                paramDescription = paramDescription.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

                var optionInfo = new CliOptionInfo
                {
                    ShortName = shortName ?? "",
                    LongName = longName ?? "",
                    Description = paramDescription,
                };

                commandInfo.Options.Add(optionInfo);
            }
        }

        // Process subcommands
        var subCommands = commandNode.Elements("Command");
        if (subCommands != null && subCommands.Any())
        {
            foreach (var subcommand in subCommands)
            {
                ProcessCommand(subcommand, fullCommandPath, level + 1, commandInfo);
            }
        }
    }

    private static string GetDescriptionText(XElement? descriptionNode)
    {
        if (descriptionNode == null)
            return "";

        // Get all text nodes within the Description element
        var textNodes = descriptionNode.DescendantNodes().OfType<XText>();
        if (textNodes != null && textNodes.Any())
        {
            var text = string.Concat(textNodes.Select(t => t.Value));
            var cleanText = CleanDescription(text);
            return cleanText.Trim();
        }
        else
        {
            var text = descriptionNode.Value;
            var cleanText = CleanDescription(text);
            return cleanText.Trim();
        }
    }

    private static readonly Regex _spectreMarkup = new(@"\[(\/?)(?!\*)[^\]]+\]", RegexOptions.Compiled);

    private static string CleanDescription(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Remove color specifications like [LightSkyBlue3]
        text = _spectreMarkup.Replace(text, "");

        return text.Trim();
    }

    public BotResponse DeserializeBotResponse(string json)
    {
        var span = json.AsSpan();

        var start = span.IndexOf('{');
        var end = span.LastIndexOf('}') + 1;

        try
        {
            var response = JsonConvert.DeserializeObject<BotResponse>(json[start..end])!;
            if (!string.IsNullOrWhiteSpace(response.Type) && response.Type[0] == '"') response.Type = response.Type.Trim('"');
            if (!string.IsNullOrWhiteSpace(response.Question) && response.Question[0] == '"') response.Question = response.Question.Trim('"');
            if (!string.IsNullOrWhiteSpace(response.Answer) && response.Answer[0] == '"') response.Answer = response.Answer.Trim('"');
            if (!string.IsNullOrWhiteSpace(response.QueryOrCommand) && response.QueryOrCommand[0] == '"') response.QueryOrCommand = response.QueryOrCommand.Trim('"');
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

    // Custom IAnsiConsole implementation
    public class StringWriterConsole(StringWriter writer) : IAnsiConsole
    {
        private readonly StringWriter _writer = writer;

        public IAnsiConsoleCursor Cursor { get; } = null!;

        public IAnsiConsoleInput Input { get; } = null!;

        public RenderPipeline Pipeline { get; } = null!;

        public IExclusivityMode ExclusivityMode => null!;

        Spectre.Console.Profile IAnsiConsole.Profile => throw new NotImplementedException();

        public void Clear(bool home) => throw new NotImplementedException();

        public void Write(Segment segment)
        {
            _writer.Write(segment.Text);
        }

        public void WriteLine()
        {
            _writer.WriteLine();
        }

        public void Write(IRenderable renderable)
        {
            var segments = renderable.Render(new RenderOptions(null!, new Spectre.Console.Size(1024, 1024)), 1024);

            // Write the segments
            foreach (var segment in segments)
            {
                Write(segment);
            }
        }
    }
}

public class BotResponse
{
    public string Answer { get; set; } = default!;
    public string Question { get; set; } = default!;
    public string QueryOrCommand { get; set; } = default!;
    public string Type { get; set; } = default!;
}