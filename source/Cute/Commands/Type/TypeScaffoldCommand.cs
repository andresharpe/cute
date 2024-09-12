using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Lib.Contentful;
using Cute.Services;
using Spectre.Console.Cli;

namespace Cute.Commands.Type;

public class TypeScaffoldCommand(IConsoleWriter console, ILogger logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings) : BaseLoggedInCommand<LoggedInSettings>(console, logger, contentfulConnection, appSettings)
{
    public override Task<int> ExecuteCommandAsync(CommandContext context, LoggedInSettings settings)
    {
        throw new NotImplementedException();
    }
}