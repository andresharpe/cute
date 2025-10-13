using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public static class CuteContentTypeTranslationContentType
{
    public static ContentType GetContentType(string locale)
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteContentTypeTranslation")
            .WithDescription("Content Type Translation Instructions.")
            .WithDisplayField("title")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

              new FieldBuilder("title", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

              new FieldBuilder("contentType", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

              new FieldBuilder("translationContext", FieldType.Text)
                    .IsRequired()
                    .Build(),
            ]);

        return contentTypeBuilder.Build();
    }
}