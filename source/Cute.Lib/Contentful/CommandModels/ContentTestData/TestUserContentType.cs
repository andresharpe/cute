using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentTestData;

public static class TestUserContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static TestUserContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("testUser")
            .WithDescription("A test model for cute development.")
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

              new FieldBuilder("name", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

             new FieldBuilder("age", FieldType.Integer)
                    .ValidateInRange(0, 150)
                    .Build(),

             new FieldBuilder("location", FieldType.Location)
                    .Build(),

             new FieldBuilder("birthDate", FieldType.Date)
                    .Build(),

            ]);

        _contentType = contentTypeBuilder.Build();
    }
}