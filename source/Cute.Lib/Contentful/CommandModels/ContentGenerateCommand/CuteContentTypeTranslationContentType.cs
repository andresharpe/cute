using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public static class CuteContentTypeTranslationContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static CuteContentTypeTranslationContentType()
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

        _contentType = contentTypeBuilder.Build();
    }
}