using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public static class CuteLanguageContentType
{
    public static ContentType GetContentType(string locale)
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

             new FieldBuilder("isContentfulLocale", FieldType.Boolean)
                    .Build(),

             new FieldBuilder("translationService", FieldType.Symbol)
                    .Build(),

             new FieldBuilder("translationContext", FieldType.Text)
                    .Build(),

             new FieldBuilder("symbolCountThreshold", FieldType.Number)
                    .Build(),

             new FieldBuilder("thresholdSetting", FieldType.Text)
                    .Build(),

            ]);

        return contentTypeBuilder.Build();
    }
}