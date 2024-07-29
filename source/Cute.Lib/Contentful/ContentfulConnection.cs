using Contentful.Core;
using Contentful.Core.Configuration;
using Cute.Lib.Exceptions;

namespace Cute.Lib.Contentful;

public class ContentfulConnection
{
    private readonly ContentfulOptions _contentfulOptions;

    private readonly ContentfulClient _contentfulClient;

    private readonly ContentfulManagementClient _contentfulManagementClient;

    public ContentfulConnection(HttpClient httpClient, IContentfulOptionsProvider optionsProvider)
    {
        _contentfulOptions = optionsProvider.GetContentfulOptions();

        if (string.IsNullOrEmpty(_contentfulOptions.ManagementApiKey))
        {
            throw new CliException($"Invalid management api key. Use 'login' command to connect to contentful first.");
        }

        if (string.IsNullOrEmpty(_contentfulOptions.DeliveryApiKey))
        {
            throw new CliException($"Invalid delivery api key. Use 'login' command to connect to contentful first.");
        }

        _contentfulClient = new ContentfulClient(httpClient, _contentfulOptions);

        if (_contentfulClient is null)
        {
            throw new CliException("Could not log into the Contentful Delivery API.");
        }

        _contentfulManagementClient = new ContentfulManagementClient(httpClient, _contentfulOptions);

        if (_contentfulManagementClient is null)
        {
            throw new CliException("Could not log into the Contentful Management API.");
        }
    }

    public ContentfulManagementClient ManagementClient => _contentfulManagementClient;
    public ContentfulClient DeliveryClient => _contentfulClient;
    public ContentfulOptions Options => _contentfulOptions;
}