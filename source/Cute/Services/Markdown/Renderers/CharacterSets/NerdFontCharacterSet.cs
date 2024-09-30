namespace Cute.Services.Markdown.Console.Renderers.CharacterSets;

public class NerdFontCharacterSet : CharacterSet
{
    public override string Zero { get; } = "";
    public override string One { get; } = "";
    public override string Two { get; } = "";
    public override string Three { get; } = "";
    public override string Four { get; } = "";
    public override string Five { get; } = "";
    public override string Six { get; } = "";
    public override string Seven { get; } = "";
    public override string Eight { get; } = "";
    public override string Nine { get; } = "";
    public override string InlineCodeOpening { get; } = "";
    public override string InlineCodeClosing { get; } = "";
    public override string ListBullet { get; } = "";
    public override string QuotePrefix { get; } = "❯";
    public override string TaskListBulletDone { get; } = "";
    public override string TaskListBulletToDo { get; } = "";
}
