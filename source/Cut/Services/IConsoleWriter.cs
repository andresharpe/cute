using Spectre.Console;

namespace Cut.Services;

public interface IConsoleWriter
{
    void WriteAlert(string text);

    void WriteAlertAccent(string text);

    void WriteDim(string text);

    void WriteBlankLine();

    void WriteHeading(string text);

    void WriteSubHeading(string text);

    void WriteNormal(string text);

    void WriteRuler();

    T Prompt<T>(IPrompt<T> prompt);
}