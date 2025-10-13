using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentJoinCommand
{
    public class CuteContentJoinType
    {
        public static ContentType GetContentType(string locale)
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

                    new FieldBuilder("sourceQueryString2", FieldType.Symbol)
                    .Build(),

                    new FieldBuilder("sourceContentType3", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                    new FieldBuilder("sourceQueryString3", FieldType.Symbol)
                    .Build(),

                ]);

            return contentTypeBuilder.Build();
        }
    }
}
