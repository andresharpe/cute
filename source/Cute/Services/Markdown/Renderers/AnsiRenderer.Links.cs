using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Spectre.Console;

namespace Cute.Services.Markdown.Console.Renderers;

public partial class AnsiRenderer
{
    private const string CannotDownloadMessage = "Cannot download image";
    private readonly HttpClient _client = new();

    private void WriteInlineLink(LinkInline link)
    {
        var label = link.Label; // ?? link.Descendants().FirstOrDefault()?.ToString();
        var url = link.Url;

        if (label is null)
        {
            // Label may be contain a series of sub items, with different formats.
            var writer = new StringWriter();
            var subRenderer = new AnsiRendererBuilder()
                .InheritSettings(_console)
                .RedirectOutput(writer)
                .Build();

            subRenderer.WriteInlines(link.Descendants<Inline>());
            label = writer.ToString();
        }

        if (label is null && url is null)
        {
            // I'm not sure this is possible.
            // Without a label and uri there is nothing to show.
            return;
        }

        // TODO: Temp fix.  Requires more thought.
        label ??= "link label missing";
        url ??= "link uri missing";

        if (link.IsImage)
        {
            WriteInlineImageLink(label, url);
        }
        else
        {
            WriteInlineTextLink(label, url);
        }
    }

    private void WriteAutoInlineLink(AutolinkInline link)
    {
        // Auto links use the url as the label.
        // Source: https://spec.commonmark.org/0.30/#autolinks
        var label = link.Url;
        var url = label;

        WriteInlineTextLink(label, url);
    }

    private void WriteInlineTextLink(string label, string url)
    {
        _console.Markup($"[{_accentColor} link={url}]{label.EscapeMarkup()}[/]");
    }

    private void WriteInlineImageLink(string label, string url)
    {
        try
        {
            var data = File.Exists(url)
                ? OpenImage(url)
                : DownloadImage(url);

            if (data is null)
            {
                throw new Exception("Cannot download image");
            }

            var image = new CanvasImage(data);
            _console.Write(image);
        }
        catch
        {
            WriteInlineLinkFallback(label, url);
        }
    }

    private Stream? OpenImage(string path) => new FileInfo(path).OpenRead();

    private Stream? DownloadImage(string url) => _client.GetStreamAsync(url).Result;

    private void WriteInlineLinkFallback(string label, string uri)
    {
        var exceptionMessage = $"Cannot create image link from uri: {uri}.  Falling back to plain text.";
        var fallbackMarkup = new Markup($"[{_accentColor} italic]{label.EscapeMarkup()}[/]");
        ThrowOrFallbackToPlainText(exceptionMessage, fallbackMarkup);
    }

    private void WriteLinkReferenceDefinitionBlock(LinkReferenceDefinitionGroup linkBlock)
    {
        foreach (var item in linkBlock)
        {
            if (item is LinkReferenceDefinition linkReference)
            {
                var escapedTitle = linkReference.Label.EscapeMarkup();
                _console.Markup($"[link={linkReference.Url}]{escapedTitle}[/]");
                continue;
            }

            // We shouldn't be able to get here.
            ThrowOrFallbackToPlainText(item);
        }
    }
}