using Contentful.Core.Models;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels.ContentTestData;

public static class TestGeoContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static TestGeoContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("testGeo")
            .WithDescription("Data about geographies.")
            .WithDisplayField("title")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .ValidateSize(2, 64)
                    .Build(),

                new FieldBuilder("title", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                new FieldBuilder("name", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

                new FieldBuilder("alternateNames", FieldType.Array)
                    .Items(
                        new SchemaBuilder(FieldType.Symbol).Build()
                    )
                    .Build(),

                new FieldBuilder("testGeoParent", FieldType.Link)
                    .ValidateLinkContentType(["testGeo"], LinkType.Entry)
                    .Build(),

                new FieldBuilder("geoType", FieldType.Symbol)
                    .IsRequired()
                    .ValidateInValues([
                        "global-region",
                        "country",
                        "state-or-province",
                        "city-or-town",
                        "neighbourhood-or-suburb",
                        "street-or-square",
                        "abstract"
                    ])
                    .Build(),

                new FieldBuilder("geoSubType", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("latLon", FieldType.Location)
                    .IsRequired()
                    .Build(),

                new FieldBuilder("ranking", FieldType.Integer)
                    .Build(),

                new FieldBuilder("population", FieldType.Integer)
                    .Build(),

                new FieldBuilder("density", FieldType.Number)
                    .Build(),

                new FieldBuilder("timeZoneStandardOffset", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("timeZoneDaylightSavingsOffset", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("wikidataQid", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("businessRationale", FieldType.Text)
                    .Build(),

                new FieldBuilder("welcomeToName", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("locationsInName", FieldType.Symbol)
                    .Build(),

                new FieldBuilder("pointsOfInterest", FieldType.Array)
                    .Items(
                        new SchemaBuilder(FieldType.Symbol).Build()
                    )
                    .Build(),

                new FieldBuilder("gallery", FieldType.Object)
                    .Build(),

            ]);

        _contentType = contentTypeBuilder.Build();
    }
}