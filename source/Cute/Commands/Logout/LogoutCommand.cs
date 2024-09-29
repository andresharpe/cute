using Cute.Commands.Login;
using Cute.Constants;
using Cute.Services;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Logout;

public class LogoutCommand(IConsoleWriter console,
    IPersistedTokenCache tokenCache) : AsyncCommand<LogoutCommand.Settings>
{
    private readonly IConsoleWriter _console = console;
    private readonly IPersistedTokenCache _tokenCache = tokenCache;

    public class Settings : LoggedInSettings
    {
        [CommandOption("--purge")]
        [Description("Specifies the content type to bulk edit.")]
        public bool Purge { get; set; } = false;
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _tokenCache.Clear(Globals.AppName);

        _console.WriteNormal("You are logged out.");

        return Task.FromResult(0);
    }
}