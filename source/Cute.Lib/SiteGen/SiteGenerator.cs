using Contentful.Core.Search;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.GraphQL;
using Cute.Lib.Scriban;
using Cute.Lib.SiteGen.Models;
using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using System.Text;

namespace Cute.Lib.SiteGen;

public class SiteGenerator
{
    private string _outputPath = string.Empty;

    private Action<FormattableString>? _displayAction;

    private readonly ContentfulConnection _contentfulConnection;

    private readonly ContentfulGraphQlClient _graphQlClient;

    public SiteGenerator(
        ContentfulConnection contentfulConnection,
        ContentfulGraphQlClient graphQlClient)
    {
        _contentfulConnection = contentfulConnection;
        _graphQlClient = graphQlClient;
    }

    public SiteGenerator WithOutputPath(string outputPath)
    {
        _outputPath = outputPath;
        return this;
    }

    public SiteGenerator WithDisplayAction(Action<FormattableString> displayAction)
    {
        _displayAction = displayAction;
        return this;
    }

    public async Task Generate(string appPlatformKey, IReadOnlyDictionary<string, string?> appSettings)
    {
        var scriptObject = CreateScriptObject(appSettings);

        var appPlatform = _contentfulConnection
            .GetPreviewEntryByKey<UiAppPlatform>("uiAppPlatform", "fields.key", appPlatformKey);

        if (appPlatform is null)
        {
            _displayAction?.Invoke($"App platform with key '{appPlatformKey}' was not found.");
            return;
        }

        await GeneratePagePerLocale(appPlatform, scriptObject);

        return;
    }

    private ScriptObject CreateScriptObject(IReadOnlyDictionary<string, string?> contentfulOptions)
    {
        ScriptObject? scriptObject = [];

        CuteFunctions.ContentfulConnection = _contentfulConnection;

        scriptObject.SetValue("cute", new CuteFunctions(), true);

        scriptObject.SetValue("config", contentfulOptions, true);

        return scriptObject;
    }

    private async Task GeneratePagePerLocale(UiAppPlatform appPlatform, ScriptObject scriptObject)
    {
        appPlatform.Brand = appPlatform.DataBrandEntry;

        foreach (var locale in appPlatform.DataLanguageEntries)
        {
            var pages = _contentfulConnection.GetPreviewEntries<UiPage>(
                new EntryQuery.Builder()
                    .WithContentType("uiPage")
                    .WithQueryConfig(b => b.LocaleIs(locale.Iso2code))
                    .Build()
                );

            appPlatform.Locale = locale.Iso2code;

            scriptObject.SetValue("app", appPlatform, true);

            await foreach (var (page, _) in pages)
            {
                _displayAction?.Invoke($"Generating {page.Key} ({page.Title}) -> Locale {locale.Iso2code}");

                var urlTemplate = Template.Parse(page.RelativeUrl);

                var urlValue = "." + RenderTemplate(scriptObject, urlTemplate);

                var html = await GeneratePage(page, scriptObject, locale.Iso2code);

                WriteHtmlFile(urlValue, html);
            }

            scriptObject.Remove("appPlatform");
        }
    }

    private void WriteHtmlFile(string urlValue, string html)
    {
        var fileName = Path.GetFullPath(Path.Combine(_outputPath, urlValue));

        var fileFolder = Path.GetDirectoryName(fileName);

        if (fileFolder is null)
        {
            _displayAction?.Invoke($"... Error: Could not create folder '{fileFolder}'");
            return;
        }

        Directory.CreateDirectory(fileFolder);

        _displayAction?.Invoke($"... writing file {fileName}");

        if (File.Exists(fileName)) File.Delete(fileName);

        File.WriteAllText(fileName, html, Encoding.UTF8);
    }

    private async Task<string> GeneratePage(UiPage page, ScriptObject scriptObject, string locale)
    {
        var headerTemplate = Template.Parse(page.HeaderComponent.HtmlSnippet);

        var footerTemplate = Template.Parse(page.FooterComponent.HtmlSnippet);

        var variables = await GetVariables(page, locale);

        var sbHtml = new StringBuilder();

        sbHtml.Append(RenderTemplate(scriptObject, headerTemplate));

        foreach (var bodyComponent in page.BodyComponents)
        {
            RenderComponent(scriptObject, sbHtml, bodyComponent);
        }

        sbHtml.Append(RenderTemplate(scriptObject, footerTemplate));

        return sbHtml.ToString();
    }

    private static void RenderComponent(ScriptObject scriptObject, StringBuilder sbHtml, UiComponent bodyComponent)
    {
        switch (bodyComponent.Sys.ContentType.SystemProperties.Id)
        {
            case "uiComponent":
                RenderUiComponent(scriptObject, sbHtml, bodyComponent);
                break;

            case "uiNavBar":
                RenderUiNavBarComponent(scriptObject, sbHtml, bodyComponent);
                break;

            case "uiNavLink":
                RenderUiNavLinkComponent(scriptObject, sbHtml, bodyComponent);
                break;

            default:
                break;
        }
    }

    private static void RenderUiNavLinkComponent(ScriptObject scriptObject, StringBuilder sbHtml, UiComponent bodyComponent)
    {
        var uiNavLinkSnippet = $"""
            <a class="TopNavigation_link__LUwc6" href="{bodyComponent.LinkUrl}">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" class="TopNavigation_icon__dWO6E">
                    <path fill="#170943"
                        d="{bodyComponent.LinkSvgPath}">
                    </path>
                </svg>
                <span class="TopNavigation_label__4S_rf">{bodyComponent.LinkLabel}</span>
            </a>
            """;

        var uiNavLinkTemplate = Template.Parse(uiNavLinkSnippet);

        sbHtml.Append(RenderTemplate(scriptObject, uiNavLinkTemplate));
    }

    private static void RenderUiNavBarComponent(ScriptObject scriptObject, StringBuilder sbHtml, UiComponent bodyComponent)
    {
        var uiNavBarSnippet = $$$"""
            <div role="navigation" class="wrapper __className_5d611a Container_containerWrapper__sfRxe">

                <div class="TopNavigation_container__tLdEc Container_container__o5zH0">

                    <a class="TopNavigation_logo__8h5UV" href="../{{ locale }}/index.html">
                        <img
                            alt="HQ_logo" loading="lazy" width="73" height="50" decoding="async" data-nimg="1"
                            style="color:transparent" src="../ark-prime-hq-assets/logo/HQ_logo_blue.svg"
                        />
                    </a>
            """;

        var uiNavLinkTemplate = Template.Parse(uiNavBarSnippet);

        sbHtml.Append(RenderTemplate(scriptObject, uiNavLinkTemplate));

        foreach (var link in bodyComponent.UiNavLinkEntries)
        {
            RenderComponent(scriptObject, sbHtml, link);
        }

        var uiNavBarSnippetClose = $$$"""
                </div>

            </div>
            """;

        var uiNavLinkTemplateClose = Template.Parse(uiNavBarSnippetClose);

        sbHtml.Append(RenderTemplate(scriptObject, uiNavLinkTemplateClose));
    }

    private static void RenderUiComponent(ScriptObject scriptObject, StringBuilder sbHtml, UiComponent bodyComponent)
    {
        var bodyTemplate = Template.Parse(bodyComponent.HtmlSnippet);

        sbHtml.Append(RenderTemplate(scriptObject, bodyTemplate));
    }

    private async Task<Dictionary<string, JArray>> GetVariables(UiPage page, string locale)
    {
        var values = new Dictionary<string, JArray>();

        if (page.UiDataQueryEntries is null) return values;

        foreach (var query in page.UiDataQueryEntries)
        {
            if (string.IsNullOrWhiteSpace(query.Query)) continue;

            var result = await _graphQlClient.GetData(query.Query, query.JsonSelector, locale);

            if (result is null) continue;

            var varName = query.VariablePrefix.Trim('.');

            values.Add(varName, result);
        }

        return values;
    }

    private static string RenderTemplate(ScriptObject scriptObject, Template template)
    {
        return template.Render(scriptObject, memberRenamer: member => member.Name.ToCamelCase());
    }
}