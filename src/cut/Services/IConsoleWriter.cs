namespace Cut.Services;

public interface IConsoleWriter
{
    void WriteAlert(string body);
    void WriteAlertAccent(string body);
    void WriteBody(string body);
    void WriteHeading(string heading);
    void WriteRuler();
}