using Contentful.Core.Models;
using Cute.Lib.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                .WithDescription("Jobs and definitions for synchronising the space with external API's.")
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
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("sourceContentType1", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("sourceKeys1", FieldType.Array)
                    .IsRequired()
                    .Items(
                        new SchemaBuilder(FieldType.Symbol)
                            .Build()
                    )
                    .Build(),

                    new FieldBuilder("sourceContentType2", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                    new FieldBuilder("sourceKeys2", FieldType.Array)
                    .IsRequired()
                    .Items(
                        new SchemaBuilder(FieldType.Symbol)
                            .Build()
                    )
                    .Build(),

                ]);

            _contentType = contentTypeBuilder.Build();
        }
    }
}
