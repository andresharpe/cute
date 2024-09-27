using Contentful.Core.Models;

namespace Cute.Lib.Contentful;

public class ContentTypeExtended : ContentType
{
    public ContentTypeExtended(ContentType contentType, int totalEntries)
    {
        SystemProperties = contentType.SystemProperties;
        Name = contentType.Name;
        Description = contentType.Description;
        DisplayField = contentType.DisplayField;
        Fields = contentType.Fields;
        TotalEntries = totalEntries;
    }

    public int TotalEntries { get; init; }
}