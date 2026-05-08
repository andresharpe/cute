using Contentful.Core;
using Contentful.Core.Configuration;

namespace Cute.Lib.Contentful;

/// <summary>
/// Production implementation of <see cref="IContentfulClientFactory"/> that wires the
/// Contentful SDK clients to a shared <see cref="HttpClient"/>.
/// </summary>
public sealed class ContentfulClientFactory : IContentfulClientFactory
{
    private readonly HttpClient _httpClient;

    public ContentfulClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public IContentfulClient CreateDeliveryClient(ContentfulOptions options)
        => new ContentfulClient(_httpClient, EnsureNotNull(options));

    public IContentfulClient CreatePreviewClient(ContentfulOptions options)
    {
        var previewOptions = Clone(EnsureNotNull(options));
        previewOptions.UsePreviewApi = true;
        return new ContentfulClient(_httpClient, previewOptions);
    }

    public IContentfulManagementClient CreateManagementClient(ContentfulOptions options)
        => new ContentfulManagementClient(_httpClient, EnsureNotNull(options));

    private static ContentfulOptions EnsureNotNull(ContentfulOptions options)
        => options ?? throw new ArgumentNullException(nameof(options));

    private static ContentfulOptions Clone(ContentfulOptions o) => new()
    {
        BaseUrl = o.BaseUrl,
        DeliveryApiKey = o.DeliveryApiKey,
        PreviewApiKey = o.PreviewApiKey,
        SpaceId = o.SpaceId,
        Environment = o.Environment,
        ManagementApiKey = o.ManagementApiKey,
        UsePreviewApi = o.UsePreviewApi,
    };
}
