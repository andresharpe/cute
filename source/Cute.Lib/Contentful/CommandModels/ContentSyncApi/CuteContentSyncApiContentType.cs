using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentSyncApi;

public class CuteContentSyncApiContentType
{
    public static ContentType GetContentType(string locale)
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteContentSyncApi")
            .WithDescription("Jobs and definitions for synchronising the space with external API's.")
            .WithDisplayField("key")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

              new FieldBuilder("order", FieldType.Integer)
                    .IsRequired()
                    .ValidateInRange(1, 999)
                    .Build(),

             new FieldBuilder("yaml", FieldType.Text)
                    .Build(),

             new FieldBuilder("sourceType", FieldType.Symbol)
                    .Build(),

            ]);

        return contentTypeBuilder.Build();
    }
}