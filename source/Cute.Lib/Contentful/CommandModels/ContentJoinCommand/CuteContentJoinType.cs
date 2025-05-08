using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentJoinCommand
{
    public class CuteContentJoinType
    {
        private static readonly ContentType _contentType;

        public static ContentType Instance()
        {
            return _contentType;
        }

        static CuteContentJoinType()
        {

            var contentTypeBuilder = new ContentTypeBuilder("cuteContentJoin")
                .WithDescription("Definitions for joining two source content types into target content type.")
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

                    new FieldBuilder("targetContentType", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                    new FieldBuilder("sourceContentType1", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                    new FieldBuilder("sourceQueryString1", FieldType.Symbol)
                    .Build(),

                    new FieldBuilder("sourceContentType2", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                    new FieldBuilder("sourceQueryString1", FieldType.Symbol)
                    .Build(),

                ]);

            _contentType = contentTypeBuilder.Build();
        }
    }
}
