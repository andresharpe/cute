using Contentful.Core.Models;

namespace Cute.Lib.SiteGen.Models;

public class UiComponent
{
    public SystemProperties Sys { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string HtmlSnippet { get; set; } = default!;
    public List<UiDataQuery> UiDataQueryEntries { get; set; } = default!;

    // NavBar
    public List<UiComponent> UiNavLinkEntries { get; set; } = default!;

    // NavLink
    public string LinkLabel { get; set; } = default!;

    public string LinkUrl { get; set; } = default!;
    public string LinkSvgPath { get; set; } = default!;
}