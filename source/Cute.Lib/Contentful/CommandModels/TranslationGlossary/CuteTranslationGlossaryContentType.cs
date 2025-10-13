using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Contentful.CommandModels.TranslationGlossary;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.Schedule
{
    public class CuteTranslationGlossaryContentType
    {
        public static ContentType GetContentType(string locale)
        {
            var contentTypeBuilder = new ContentTypeBuilder(nameof(CuteTranslationGlossary).ToCamelCase())
                .WithDescription("Glossary for translations.")
                .WithDisplayField("title")
                .WithFields([

                    new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("title", FieldType.Symbol)
                        .Build()

                ]);

            return contentTypeBuilder.Build();
        }
    }
}
