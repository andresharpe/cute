using Contentful.Core.Models;
using Contentful.Core.Search;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.Schedule
{
    public class CuteTranslationContentType
    {
        private static readonly ContentType _contentType;

        public static ContentType Instance()
        {
            return _contentType;
        }

        static CuteTranslationContentType()
        {
            var contentTypeBuilder = new ContentTypeBuilder(nameof(CuteTranslationContentType).ToCamelCase())
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
