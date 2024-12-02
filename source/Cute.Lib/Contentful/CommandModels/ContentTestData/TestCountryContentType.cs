#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentTestData;

public static class TestCountryContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static TestCountryContentType()
    {
        var contentTypeBuilder = new ContentTypeBuilder("testCountry")
            .WithDescription("Data about countries.")
            .WithDisplayField("title")
            .WithFields([

                new FieldBuilder("key", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .ValidateRegex(new Regex(@"^[A-Z_]+$").ToString())
                    .Build(),

                new FieldBuilder("title", FieldType.Symbol)
                    .IsRequired()
                    .IsUnique()
                    .Build(),

                new FieldBuilder("name", FieldType.Symbol)
                    .IsLocalized()
                    .IsRequired()
                    .Build(),

                new FieldBuilder("iso2code", FieldType.Symbol)
                    .IsRequired()
                    .ValidateRegex(new Regex(@"^[A-Z]{2}$").ToString())
                    .ValidateSize(2,2)
                    .Build(),

                new FieldBuilder("iso3code", FieldType.Symbol)
                    .IsRequired()
                    .ValidateRegex(new Regex(@"^[A-Z]{3}$").ToString())
                    .ValidateSize(3,3)
                    .Build(),

                new FieldBuilder("cuteLanguageEntries", FieldType.Array)
                    .IsRequired()
                    .Items(
                        new SchemaBuilder(FieldType.Link, LinkType.Entry)
                            .ValidateLinkContentType(["cuteLanguage"])
                            .Build()
                    )
                    .Build(),

                new FieldBuilder("dialingCodes", FieldType.Array)
                    .IsRequired()
                    .Items(
                        new SchemaBuilder(FieldType.Symbol)
                            .ValidateRegex(new Regex(@"^\+[0-9]{1,10}$").ToString())
                            .Build()
                    )
                    .Build(),

                new FieldBuilder("latLon", FieldType.Location)
                    .Build(),

                new FieldBuilder("flagSvgUrl", FieldType.Symbol)
                    .IsRequired()
                    .ValidateUnique()
                    .ValidateRegex(new Regex(@"^(ftp|http|https):\/\/(\w+:{0,1}\w*@)?(\S+)(:[0-9]+)?(\/|\/([\w#!:.?+=&%@!\-/]))?$").ToString())
                    .Build(),

                new FieldBuilder("workingDays", FieldType.Array)
                    .Items(
                        new SchemaBuilder(FieldType.Symbol)
                            .ValidateInValues(["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"])
                            .Build()
                    )
                    .Build(),

                new FieldBuilder("currencies", FieldType.Array)
                    .IsRequired()
                    .Items(
                        new SchemaBuilder(FieldType.Symbol)
                            .ValidateRegex(new Regex(@"^[A-Z]{3}$").ToString())
                            .Build()
                    )
                    .Build(),

                new FieldBuilder("lastLocationCount", FieldType.Integer)
                    .Build(),

                new FieldBuilder("testGeoEntry", FieldType.Link)
                    .ValidateLinkContentType(["testGeo"], LinkType.Entry)
                    .Build(),

                new FieldBuilder("population", FieldType.Integer)
                    .Build(),

                new FieldBuilder("wikidataQid", FieldType.Symbol)
                    .Build(),

            ]);

        _contentType = contentTypeBuilder.Build();
    }
}