using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.GraphQL;
using Cute.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace Cute.Unit.Tests;

public class AutoGraphQlQueryBuilderTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    private readonly AppSettings _appSettings;

    private readonly ContentfulConnection _contentfulConnection;

    public AutoGraphQlQueryBuilderTests()
    {
        _dataProtectionProvider = DataProtectionProvider.Create(Globals.AppName);

        _appSettings = new PersistedTokenCache(_dataProtectionProvider)
            .LoadAsync(Globals.AppName)
            .Result!;

        _contentfulConnection = new ContentfulConnection(new HttpClient(), _appSettings);
    }

    [Fact]
    public void ExtractScribanVariablesFromQuery_Returns_ExtractedVariables()
    {
        string templateContent = """
            {{contentGeo.name}} Widgets to buy in {{contentGeo.dataGeoEntry.title}} - {{ contentGeo.dataBrandEntry.key }} | Blah, blah, blah
            """;

        var builder = new AutoGraphQlQueryBuilder(_contentfulConnection)
            .WithTemplateContent(templateContent);

        var success = builder.TryBuildQuery(out var query);

        var expectedQuery = """
            query GetContent($locale: String, $preview: Boolean, $skip: Int, $limit: Int) {
              contentGeoCollection(locale: $locale, preview: $preview, skip: $skip, limit: $limit) {
                items {
                  sys { id }
                  name
                  dataGeoEntry {
                    sys { id }
                    title
                  }
                  dataBrandEntry {
                    sys { id }
                    key
                  }
                }
              }
            }
            """;

        success.Should().BeTrue();
        builder.Errors.Should().BeEmpty();
        query.Should().NotBeNullOrWhiteSpace();
        query.Should().BeEquivalentTo(expectedQuery);
    }
}