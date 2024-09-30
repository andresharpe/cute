namespace Cute.Services.Markdown.Console.Renderers.CharacterSets;

public abstract class CharacterSet
{
    public virtual string Zero { get; } = "0";
    public virtual string One { get; } = "1";
    public virtual string Two { get; } = "2";
    public virtual string Three { get; } = "3";
    public virtual string Four { get; } = "4";
    public virtual string Five { get; } = "5";
    public virtual string Six { get; } = "6";
    public virtual string Seven { get; } = "7";
    public virtual string Eight { get; } = "8";
    public virtual string Nine { get; } = "9";
    public virtual string InlineCodeOpening { get; } = "";
    public virtual string InlineCodeClosing { get; } = "";
    public virtual string ListBullet { get; } = "";
    public virtual string QuotePrefix { get; } = "❯";
    public virtual string TaskListBulletDone { get; } = "[x]";
    public virtual string TaskListBulletToDo { get; } = "[ ]";
}
