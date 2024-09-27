using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Services;
using Spectre.Console.Cli;

namespace Cute.Commands.App;

public class AppGenerateCommand(IConsoleWriter console, ILogger logger,
    AppSettings appSettings) : BaseLoggedInCommand<LoggedInSettings>(console, logger, appSettings)
{
    public override Task<int> ExecuteCommandAsync(CommandContext context, LoggedInSettings settings)
    {
        throw new NotImplementedException();
    }
}