using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Services;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace Cute.Unit.Tests;

public class ContentfulConnectionTests
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    private readonly AppSettings _appSettings;

    private readonly string _openAiEndpoint;

    private readonly string _openAiApiKey;

    private readonly HttpClient _httpClient;

    public ContentfulConnectionTests()
    {
        _dataProtectionProvider = DataProtectionProvider.Create(Globals.AppName);

        _appSettings = new PersistedTokenCache(_dataProtectionProvider)
            .LoadAsync(Globals.AppName)
            .Result!;

        _openAiEndpoint = _appSettings.OpenAiEndpoint;

        _openAiApiKey = _appSettings.OpenAiApiKey;

        _httpClient = new HttpClient();
    }

    [Fact]
    public async Task CreateConnectionReturnsAWorkingConnection()
    {
        var conn = new ContentfulConnection.Builder()
            .WithHttpClient(_httpClient)
            .WithOptionsProvider(_appSettings)
            .Build();

        var spaces = await conn.GetSpacesAsync();

        spaces.Should().NotBeEmpty();

        var contentTypes = await conn.GetContentTypesAsync();

        contentTypes.Should().NotBeEmpty();

        var contentTypesCopy = await conn.GetContentTypesAsync();

        contentTypesCopy.Should().NotBeEmpty();
        contentTypes.Should().BeSameAs(contentTypesCopy);

        var counts = await conn.GetContentTypeExtendedAsync();

        counts.Should().NotBeEmpty();
        counts.Should().HaveCount(contentTypes.Count());
    }
}