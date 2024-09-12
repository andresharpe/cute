#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentTestData;

public static class TestLocationContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static TestLocationContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("testLocation")
            .WithDescription("Data about locations.")
            .WithDisplayField("title")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .ValidateRegex(new Regex(@"^\d{1,6}$").ToString())
                    .ValidateSize(1,6)
                    .Build(),

                new FieldBuilder("title", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                new FieldBuilder("name", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

                new FieldBuilder("uniqueName", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

                new FieldBuilder("locationNumber", FieldType.Integer)
                    .IsRequired()
                    .IsUnique()
                    .ValidateInRange(1, null)
                    .Build(),

                new FieldBuilder("imageGallery", FieldType.Object)
                    .Build(),

                new FieldBuilder("address1", FieldType.Symbol)
                    .IsLocalized()
                    .Build(),

                new FieldBuilder("address2", FieldType.Symbol)
                    .IsLocalized()
                    .Build(),

                new FieldBuilder("address3", FieldType.Symbol)
                    .IsLocalized()
                    .Build(),

                new FieldBuilder("testCountryEntry", FieldType.Link)
                    .ValidateLinkContentType(["testCountry"], LinkType.Entry)
                    .Build(),

                new FieldBuilder("zipPostCode", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("neutralDescription", FieldType.Text)
                    .Build(),

                new FieldBuilder("latLong", FieldType.Location)
                    .Build(),

                new FieldBuilder("receptionOpenTimes", FieldType.Object)
                    .Build(),

                new FieldBuilder("openDate", FieldType.Date)
                    .Build(),

                new FieldBuilder("closeDate", FieldType.Date)
                    .Build(),

                new FieldBuilder("isTwentyFourSevenAccessible", FieldType.Boolean)
                    .Build(),

            ]);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

        _contentType = contentTypeBuilder.Build();
    }
}