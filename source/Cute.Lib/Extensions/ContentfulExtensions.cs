using Contentful.Core.Models;
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
}