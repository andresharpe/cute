using ColorfulCode;
using Cute.Services.Markdown.SyntaxHighlighters;
using Spectre.Console;
using System.Diagnostics.CodeAnalysis;

namespace Cute.Services.Markdown.Console.SyntaxHighlighters;

/// <summary>
/// Provides very basic syntax highlighting.  Recommend as a fallback option, should other
/// highlighters fail.
/// </summary>
public class BasicSyntaxHighlighter : ISyntaxHighlighter
{
    private static readonly SyntaxSet _syntaxSet = SyntaxSet.LoadDefaults();

    private static readonly ThemeSet _themeSet = ThemeSet.LoadDefaults();

    /// <inheritdoc/>
    public bool TryGetHighlightSyntax(
        string code,
        string? language,
        [NotNullWhen(returnValue: true)]
        out string[] highlightedCode)
    {
        language ??= "txt";
        var mappedLanguage = _languageMap.GetValueOrDefault(language, language);

        string? markup;
        try
        {
            var syntax = _syntaxSet.FindByExtension(mappedLanguage);
            var theme = _themeSet["base16-mocha.dark"];
            var html = syntax.HighlightToHtml(code, theme);
            markup = HtmlToSpectreConverter.ConvertHtmlToMarkup(html);
        }
        catch
        {
            markup = code.EscapeMarkup();
        }

        var lineNumber = 1;
        var lines = markup.Replace("\r\n", "\n").Split('\n');

        highlightedCode = new string[lines.Length];

        if (lines.Length == 1)
        {
            var line = lines[0];
            highlightedCode[0] = $" {line}";
            return true;
        }

        for (var i = 0; i < highlightedCode.Length; i++)
        {
            var line = lines[i];
            var rightAlignedLineNumber = lineNumber++.ToString().PadLeft(4);
            highlightedCode[i] = $"[grey]{rightAlignedLineNumber}[/] {line}";
        }

        return true;
    }

    private static readonly Dictionary<string, string>
        _languageMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["plaintext"] = "txt",
            ["asp"] = "asp",
            ["html_asp"] = "asp",
            ["actionscript"] = "as",
            ["applescript"] = "applescript",
            ["batchfile"] = "bat",
            ["nant"] = "build",
            ["csharp"] = "cs",
            ["cpp"] = "cpp",
            ["c"] = "c",
            ["css"] = "css",
            ["clojure"] = "clj",
            ["d"] = "d",
            ["diff"] = "diff",
            ["erlang"] = "erl",
            ["html_erlang"] = "html",
            ["go"] = "go",
            ["graphviz"] = "dot",
            ["groovy"] = "groovy",
            ["html"] = "html",
            ["haskell"] = "hs",
            ["literate_haskell"] = "lhs",
            ["jsp"] = "jsp",
            ["java"] = "java",
            ["javadoc"] = "java",
            ["java_properties"] = "properties",
            ["json"] = "json",
            ["javascript"] = "js",
            ["regex_js"] = "js",
            ["bibtex"] = "bib",
            ["latex_log"] = "log",
            ["kotlin"] = "c",
            ["latex"] = "tex",
            ["tex"] = "tex",
            ["lisp"] = "lisp",
            ["lua"] = "lua",
            ["make_output"] = "txt",
            ["makefile"] = "makefile",
            ["markdown"] = "md",
            ["multimarkdown"] = "md",
            ["matlab"] = "m",
            ["ocaml"] = "ml",
            ["ocamllex"] = "mll",
            ["ocamlyacc"] = "mly",
            ["camlp4"] = "ml",
            ["objc"] = "m",
            ["objc++"] = "mm",
            ["php_source"] = "php",
            ["php"] = "php",
            ["pascal"] = "pas",
            ["perl"] = "pl",
            ["python"] = "py",
            ["regex_py"] = "py",
            ["r_console"] = "r",
            ["r"] = "r",
            ["rd_r_doc"] = "rd",
            ["html_rails"] = "html",
            ["javascript_rails"] = "js",
            ["ruby_haml"] = "haml",
            ["rails"] = "rb",
            ["sql_rails"] = "sql",
            ["regex"] = "regex",
            ["rst"] = "rst",
            ["ruby"] = "rb",
            ["cargo_build_results"] = "txt",
            ["rust"] = "rs",
            ["sql"] = "sql",
            ["scala"] = "scala",
            ["bash"] = "sh",
            ["shell_unix_generic"] = "sh",
            ["commands_builtin_shell_bash"] = "sh",
            ["html_tcl"] = "html",
            ["tcl"] = "tcl",
            ["textile"] = "textile",
            ["xml"] = "xml",
            ["yaml"] = "json",
            ["typescript"] = "js"
        };
}