namespace Cute.Lib.Contentful;

public class ContentLocales
{
    public string[] Locales { get; init; }

    public string DefaultLocale { get; init; }

    public ContentLocales(string[] locales, string defaultLocale)
    {
        defaultLocale = defaultLocale.ToLower();

        DefaultLocale = defaultLocale;

        Locales = locales
            .Select(l => l.ToLower())
            .Where(l => !l.Equals(defaultLocale))
            .OrderBy(l => l)
            .ToArray();
    }

    public string[] GetAllLocales()
    {
        return [DefaultLocale, .. Locales];
    }
}