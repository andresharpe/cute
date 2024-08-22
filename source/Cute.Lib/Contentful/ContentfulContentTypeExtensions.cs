using Contentful.Core;
using Contentful.Core.Models;
using Cute.Lib.Extensions;

namespace Cute.Lib.Contentful;

public static class ContentfulContentTypeExtensions
{
    public static async Task CreateWithId(this ContentType contentType,
        ContentfulManagementClient client, string contentTypeId)
    {
        if (contentType is null) return;

        contentType.Name = contentType.Name
            .RemoveEmojis()
            .Trim();

        if (contentType.SystemProperties.Id != contentTypeId)
        {
            contentType.SystemProperties.Id = contentTypeId;
            contentType.Name = contentTypeId.CamelToPascalCase();
        }

        // Temp hack: Contentful API does not yet understand Taxonomy Tags

        contentType.Metadata = null;

        // end: hack

        await client.CreateOrUpdateContentType(contentType);

        await client.ActivateContentType(contentTypeId, 1);
    }
}