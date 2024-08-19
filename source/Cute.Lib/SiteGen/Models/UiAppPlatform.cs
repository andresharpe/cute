namespace Cute.Lib.SiteGen.Models;

public class UiAppPlatform
{
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public DataBrand DataBrandEntry { get; set; } = default!;
    public List<DataLanguage> DataLanguageEntries { get; set; } = default!;
    public string HomeUrlForProd { get; set; } = default!;
    public string HomeUrlForUat { get; set; } = default!;
    public string HomeUrlForTest { get; set; } = default!;

    // Context Vars
    public string Locale { get; set; } = default!;

    public DataBrand Brand { get; set; } = default!;
}