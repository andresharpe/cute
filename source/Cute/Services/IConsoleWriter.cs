using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Cute.Services;

public interface IConsoleWriter
{
    ILogger? Logger { get; set; }

    void WriteAlert(string text);

    void WriteAlertAccent(string text);

    void WriteDim(string text);

    void WriteBlankLine();

    void Write(Renderable renderable);

    void WriteLine();

    void WriteHeading(string text);

    void WriteHeading(string textTemplate, params object?[] values);

    void WriteSubHeading(string text);

    void WriteNormal(string text);

    void WriteNormal(string textTemplate, params object?[] values);

    void WriteRuler();

    T Prompt<T>(IPrompt<T> prompt);

    void WriteLine(string v, Style styleAlert);

    void WriteException(Exception ex);

    void WriteNormalWithHighlights(FormattableString text, Style highlightStyle);

    void WriteHeadingWithHighlights(FormattableString text, Style highlightStyle);
    string FormatToMarkup(FormattableString text, Style? normalStyle = null, Style? highlightStyle = null);
    void WriteTable(JArray jArray);
}