using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteDataQueryContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static CuteDataQueryContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteDataQuery")
            .WithDescription("A graphQl query that returns Contentful entries.")
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

            new FieldBuilder("query", FieldType.Text)
                .IsRequired()
                .Build(),

            new FieldBuilder("jsonSelector", FieldType.Symbol)
                .Build(),

            new FieldBuilder("variablePrefix", FieldType.Symbol)
                .IsRequired()
                .Build(),

            ]);

        _contentType = contentTypeBuilder.Build();
    }
}