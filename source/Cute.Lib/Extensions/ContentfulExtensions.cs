using Contentful.Core.Models;
using Cute.Lib.Enums;
using FuzzySharp;

namespace Cute.Lib.Extensions;

public static class ContentfulExtensions
{
    public static string Id<T>(this Entry<T> entry)
    {
        return entry.SystemProperties.Id;
    }

    public static string Id(this IContentfulResource resource)
    {
        return resource.SystemProperties.Id;
    }

    public static int? Version<T>(this Entry<T> entry)
    {
        return entry.SystemProperties.Version;
    }

    public static int? Version(this IContentfulResource resource)
    {
        return resource.SystemProperties.Version;
    }

    public static string BestFieldMatch(this ContentType contentType, string input)
    {
        return contentType.Fields
            .OrderByDescending(f =>
                Fuzz.PartialRatio(input, f.Id)
            )
            .First().Id;
    }

    public static EntryState? GetEntryState(this SystemProperties sys)
    {
        if (sys.ArchivedVersion != null) return EntryState.Archived;
        if (sys.PublishedVersion != null && sys.Version == sys.PublishedVersion + 1) return EntryState.Published;
        if (sys.PublishedVersion != null && sys.Version >= sys.PublishedVersion + 2) return EntryState.Changed;
        if (sys.PublishedVersion == null || sys.PublishedVersion == 0) return EntryState.Draft;
        return null;
    }
}