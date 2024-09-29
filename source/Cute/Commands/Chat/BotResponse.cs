namespace Cute.Commands.Chat;

public class BotResponse
{
    public string Answer { get; set; } = default!;
    public string Question { get; set; } = default!;
    public string QueryOrCommand { get; set; } = default!;
    public string Type { get; set; } = default!;
}