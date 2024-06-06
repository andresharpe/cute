using Cut.Lib.Enums;

namespace Cut.Lib.CommandRunners;

public class CommandRunnerResult(RunnerResult result, string? message = null)
{
    private readonly RunnerResult _result = result;
    private readonly string? _message = message;

    public RunnerResult Result => _result;

    public string Message => _message ?? "Unknown";
}