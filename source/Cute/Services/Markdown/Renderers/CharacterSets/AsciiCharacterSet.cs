namespace Cute.Services.Markdown.Console.Renderers.CharacterSets;

public class AsciiCharacterSet : CharacterSet
{
    public override string Zero { get; } = "0";
    public override string One { get; } = "1";
    public override string Two { get; } = "2";
    public override string Three { get; } = "3";
    public override string Four { get; } = "4";
    public override string Five { get; } = "5";
    public override string Six { get; } = "6";
    public override string Seven { get; } = "7";
    public override string Eight { get; } = "8";
    public override string Nine { get; } = "9";
    public override string InlineCodeOpening { get; } = "«";
    public override string InlineCodeClosing { get; } = "»";
    public override string ListBullet { get; } = "•";
    public override string QuotePrefix { get; } = "»";
    public override string TaskListBulletDone { get; } = "[✓]";
    public override string TaskListBulletToDo { get; } = "[ ]";
}