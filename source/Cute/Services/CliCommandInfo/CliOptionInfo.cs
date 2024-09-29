namespace Cute.Services.CliCommandInfo;

public class CliOptionInfo
{
    public string ShortName { get; set; } = default!;
    public string LongName { get; set; } = default!;
    public string Description { get; set; } = default!;

    public override bool Equals(object? obj)
    {
        if (obj is CliOptionInfo option)
        {
            return ShortName == option.ShortName && LongName == option.LongName && Description == option.Description;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return (ShortName, LongName, Description).GetHashCode();
    }
}