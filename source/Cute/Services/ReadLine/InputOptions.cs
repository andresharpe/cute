using System.Drawing;

namespace Cute.Services.ReadLine;

public static partial class MultiLineConsoleInput
{
    public struct InputOptions
    {
        public string Prompt { get; set; } = "> ";
        public Color PromptForeground = Color.DarkOrange;
        public Color TextForeground = Color.FromArgb(192, 192, 192);
        public Color TextBackground = Color.FromArgb(40, 40, 40);

        public InputOptions()
        {
        }
    }
}