﻿using Contentful.Core;
using Contentful.Core.Configuration;
using Cute.Lib.Exceptions;

namespace Cute.Lib.Contentful;

public class LegacyContentfulConnection
{
    private readonly ContentfulOptions _contentfulOptions;

    private readonly ContentfulClient _contentfulClient;

    private readonly ContentfulClient _contentfulPreviewClient;

    private readonly ContentfulManagementClient _contentfulManagementClient;

    public LegacyContentfulConnection(HttpClient httpClient, IContentfulOptionsProvider optionsProvider)
    {
        _contentfulOptions = optionsProvider.GetContentfulOptions();

        if (string.IsNullOrEmpty(_contentfulOptions.ManagementApiKey))
        {
            throw new CliException($"Invalid management API key. Use 'login' command to connect to Contentful first.");
        }

        if (string.IsNullOrEmpty(_contentfulOptions.DeliveryApiKey))
        {
            throw new CliException($"Invalid delivery API key. Use 'login' command to connect to Contentful first.");
        }

        _contentfulClient = new ContentfulClient(httpClient, _contentfulOptions);

        if (_contentfulClient is null)
        {
            throw new CliException("Could not log into the Contentful Delivery API.");
        }

        var previewOptions = new ContentfulOptions
        {
            DeliveryApiKey = _contentfulOptions.DeliveryApiKey,
            PreviewApiKey = _contentfulOptions.PreviewApiKey,
            ManagementApiKey = _contentfulOptions.ManagementApiKey,
            Environment = _contentfulOptions.Environment,
            SpaceId = _contentfulOptions.SpaceId,
            ResolveEntriesSelectively = _contentfulOptions.ResolveEntriesSelectively,
            UsePreviewApi = true,
        };

        _contentfulPreviewClient = new ContentfulClient(httpClient, previewOptions);

        if (_contentfulPreviewClient is null)
        {
            throw new CliException("Could not log into the Contentful Preview API.");
        }

        _contentfulManagementClient = new ContentfulManagementClient(httpClient, _contentfulOptions);

        if (_contentfulManagementClient is null)
        {
            throw new CliException("Could not log into the Contentful Management API.");
        }
    }

    public ContentfulManagementClient ManagementClient => _contentfulManagementClient;
    public ContentfulClient DeliveryClient => _contentfulClient;
    public ContentfulClient PreviewClient => _contentfulPreviewClient;
    public ContentfulOptions Options => _contentfulOptions;
}