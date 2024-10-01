namespace Cute.Services.Markdown.SyntaxHighlighters;

using HtmlAgilityPack;
using Spectre.Console;
using System.Text;

public class HtmlToSpectreConverter
{
    public static string ConvertHtmlToMarkup(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        StringBuilder sb = new StringBuilder();

        foreach (var node in doc.DocumentNode.ChildNodes)
        {
            ParseNode(node, sb);
        }

        return sb.ToString();
    }

    private static void ParseNode(HtmlNode node, StringBuilder sb)
    {
        if (node.Name == "span")
        {
            var style = node.GetAttributeValue("style", string.Empty);
            if (style.Contains("color:"))
            {
                var color = ExtractColor(style);
                sb.Append($"[{color}]");
            }
        }
        else if (node.Name == "pre" || node.Name == "#text")
        {
            foreach (var subNode in node.ChildNodes)
            {
                ParseNode(subNode, sb);
            }
            return;
        }

        sb.Append(HtmlEntity.DeEntitize(node.InnerText).EscapeMarkup());

        if (node.Name == "span" && node.GetAttributeValue("style", "").Contains("color:"))
        {
            sb.Append("[/]");
        }
    }

    private static string ExtractColor(string style)
    {
        // Extract color from inline style
        int colorStart = style.IndexOf("color:") + 6;
        int colorEnd = style.IndexOf(';', colorStart);
        return style.Substring(colorStart, colorEnd - colorStart).Trim();
    }
}