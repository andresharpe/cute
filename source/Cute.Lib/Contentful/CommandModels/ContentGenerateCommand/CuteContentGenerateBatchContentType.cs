#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteContentGenerateBatchContentType
{
    public static ContentType GetContentType(string locale)
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteContentGenerateBatch")
            .WithDescription("Batch requests for AI to generate and translate content for any content type and field.")
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

                new FieldBuilder("cuteContentGenerateEntry", FieldType.Link)
                    .IsRequired()
                    .ValidateLinkContentType(["cuteContentGenerate"], LinkType.Entry)
                    .Build(),

                new FieldBuilder("status", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("createdAt", FieldType.Date)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("completedAt", FieldType.Date)
                    .Build(),

                new FieldBuilder("appliedAt", FieldType.Date)
                    .Build(),

                new FieldBuilder("cancelledAt", FieldType.Date)
                    .Build(),

                new FieldBuilder("failedAt", FieldType.Date)
                    .Build(),

                new FieldBuilder("expiredAt", FieldType.Date)
                    .Build(),

                new FieldBuilder("targetContentType", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("targetField", FieldType.Symbol)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("targetEntriesCount", FieldType.Integer)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("completionTokens", FieldType.Integer)
                    .Build(),

                new FieldBuilder("promptTokens", FieldType.Integer)
                    .Build(),

                new FieldBuilder("totalTokens", FieldType.Integer)
                    .Build(),

            ]);

        return contentTypeBuilder.Build();
    }
}