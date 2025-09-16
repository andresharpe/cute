using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Contentful.CommandModels.TranslationGlossary;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.Schedule
{
    public class CuteTranslationGlossaryContentType
    {
        private static readonly ContentType _contentType;

        public static ContentType Instance()
        {
            return _contentType;
        }

        static CuteTranslationGlossaryContentType()
        {
            var contentTypeBuilder = new ContentTypeBuilder(nameof(CuteTranslationGlossary).ToCamelCase())
                .WithDescription("Glossary for translations.")
                .WithDisplayField("title")
                .WithFields([

                    new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("title", FieldType.Text)
                        .IsUnique()
                        .Build()

                ]);

            _contentType = contentTypeBuilder.Build();
        }
    }
}
