using Cute.Lib.InputAdapters.Http;
using FluentAssertions;

namespace Cute.Unit.Tests;

public class HttpInputAdapterUserAgentTests
{
    [Fact]
    public void ResolveUserAgent_Should_ProduceADescriptiveHeaderValue()
    {
        var userAgent = HttpInputAdapter.ResolveUserAgent();

        userAgent.Should().StartWith("cute/");
        userAgent.Should().Contain("https://github.com/andresharpe/cute");
    }

    [Fact]
    public void ResolveUserAgent_Should_ProduceAValueHttpClientAccepts()
    {
        using var httpClient = new HttpClient();

        var act = () => httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(HttpInputAdapter.ResolveUserAgent());

        act.Should().NotThrow();
        httpClient.DefaultRequestHeaders.Contains("User-Agent").Should().BeTrue();
    }
}
