using Cute.Services.Markdown.Console.Renderers;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Converters;

internal static class ColorSystemSupportConverter
{
    internal static ColorSystemSupport FromColorSystem(ColorSystem colorSystem)
    {
        return (colorSystem) switch
        {
            ColorSystem.EightBit => ColorSystemSupport.EightBit,
            ColorSystem.Legacy => ColorSystemSupport.Legacy,
            ColorSystem.NoColors => ColorSystemSupport.NoColors,
            ColorSystem.Standard => ColorSystemSupport.Standard,
            ColorSystem.TrueColor => ColorSystemSupport.TrueColor,
            _ => ColorSystemSupport.Detect
        };
    }
}
