namespace Cute.Lib.SiteGen.Models;

public class UiPage
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public UiAppPlatform UiAppPlatformEntry { get; set; } = default!;
    public string RelativeUrl { get; set; } = default!;
    public UiComponent HeaderComponent { get; set; } = default!;
    public List<UiComponent> BodyComponents { get; set; } = default!;
    public UiComponent FooterComponent { get; set; } = default!;
    public List<UiDataQuery> UiDataQueryEntries { get; set; } = default!;
}