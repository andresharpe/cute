using Cute.Services;
using Spectre.Console.Cli;

namespace Cute.Commands.Version;

public sealed class VersionCommand : AsyncCommand<VersionCommand.Settings>
{
    private readonly IConsoleWriter _console;

    public VersionCommand(IConsoleWriter console)
    {
        _console = console;
    }

    public class Settings : CommandSettings
    {
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var installedVersion = VersionChecker.GetInstalledCliVersion();

        _console.WriteNormal(installedVersion);

        return Task.FromResult(0);
    }
}