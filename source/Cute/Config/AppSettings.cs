using Contentful.Core.Configuration;
using Cute.Constants;
using Cute.Lib.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Extensions;
using System.Runtime.Serialization;

namespace Cute.Config;

public class AppSettings : IContentfulOptionsProvider
{
    public string ContentfulDefaultSpace { get; set; } = default!;
    public string ContentfulDefaultEnvironment { get; set; } = default!;
    public string ContentfulManagementApiKey { get; set; } = default!;
    public string ContentfulDeliveryApiKey { get; set; } = default!;
    public string ContentfulPreviewApiKey { get; set; } = default!;
    public string OpenAiEndpoint { get; set; } = default!;
    public string OpenAiApiKey { get; set; } = default!;
    public string OpenAiDeploymentName { get; set; } = default!;
    public string OpenTelemetryEndpoint { get; set; } = default!;
    public string OpenTelemetryApiKey { get; set; } = default!;
    public string AzureTranslatorApiKey { get; set; } = default!;
    public string AzureTranslatorEndpoint { get; set; } = default!;
    public string AzureTranslatorRegion { get; set; } = default!;

    [OnDeserialized]
    internal void SetFromEnvironment(StreamingContext context)
    {
        SetFromEnvironment();
    }

    internal AppSettings SetFromEnvironment()
    {
        var envValues = EnvironmentVars.GetAll();

        var prefix = $"{Globals.AppName.CamelToPascalCase()}__";

        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            if (envValues.TryGetValue($"{prefix}{prop.Name}", out var value))
            {
                prop.SetValue(this, value);
            }
        }
        return this;
    }

    public ContentfulOptions GetContentfulOptions()
    {
        return new ContentfulOptions()
        {
            ManagementApiKey = ContentfulManagementApiKey,
            SpaceId = ContentfulDefaultSpace,
            DeliveryApiKey = ContentfulDeliveryApiKey,
            PreviewApiKey = ContentfulPreviewApiKey,
            Environment = ContentfulDefaultEnvironment,
            ResolveEntriesSelectively = true,
        };
    }
}