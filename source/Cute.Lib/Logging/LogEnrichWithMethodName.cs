using Serilog.Context;
using System.Runtime.CompilerServices;

namespace Cute.Lib.Logging;

internal class LogEnrichWithMethodName : IDisposable
{
    private readonly IDisposable _disposable;

    public LogEnrichWithMethodName([CallerMemberName] string memberName = "")
    {
        _disposable = LogContext.PushProperty("CallerMemberName", memberName);
    }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}