using Markdig;
using Markdig.Syntax;

namespace Cute.Services.Markdown.Console.Parsers;

/// <summary>
/// Parses raw markdown text and returns an abstract-syntax-tree representation.
/// </summary>
public class MarkdownParser
{
    private readonly static MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Converts markdown into a document.
    /// /// </summary>
    /// <param name="markdown">Raw markdown text</param>
    /// <returns>ABT markdown document</returns>
    public MarkdownDocument ConvertToMarkdownDocument(string markdown)
    {
        return Markdig.Markdown.Parse(markdown, _markdownPipeline);
    }
}
