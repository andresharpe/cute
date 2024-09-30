using Spectre.Console;

namespace Cute.Services.Markdown.Console.Converters;

internal static class AnsiSupportConverter
{
    internal static AnsiSupport FromAnsiSupported(bool ansiSupported) =>
        ansiSupported
            ? AnsiSupport.Yes
            : AnsiSupport.No;
}
