using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public static class CuteLanguageContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static CuteLanguageContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteLanguage")
            .WithDescription("Data about languages.")
            .WithDisplayField("title")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .ValidateRegex(new Regex(@"^[a-z]{2,2}$").ToString())
                    .Build(),

              new FieldBuilder("title", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

              new FieldBuilder("name", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

              new FieldBuilder("nativeName", FieldType.Symbol)
                    .Build(),

             new FieldBuilder("iso2code", FieldType.Symbol)
                    .IsRequired()
                    .ValidateRegex(new Regex(@"^[a-z]{2,2}$").ToString())
                    .Build(),

             new FieldBuilder("wikidataQid", FieldType.Symbol)
                    .Build(),

            ]);

        _contentType = contentTypeBuilder.Build();
    }
}