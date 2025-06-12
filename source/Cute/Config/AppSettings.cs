﻿using Buttercup.Core.Services.Implementation;
using Contentful.Core.Configuration;
using Cute.Constants;
using Cute.Lib.AiModels;
using Cute.Lib.AzureOpenAi;
using Cute.Lib.Config;
using Cute.Lib.Contentful;
using Cute.Lib.Extensions;
using System.Runtime.Serialization;

namespace Cute.Config;

public class AppSettings : IContentfulOptionsProvider, IAzureOpenAiOptionsProvider, IDatabaseSettings
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
    public string DatabaseProvider { get; set; } = default!;
    public string DatabaseConnectionString { get; set; } = default!;

    [OnDeserialized]
    internal void GetFromEnvironment(StreamingContext context)
    {
        GetFromEnvironment();
    }

    internal AppSettings GetFromEnvironment()
    {
        var prefix = $"{Globals.AppName.CamelToPascalCase()}__";

        var envValues = EnvironmentVars.GetAll().Where(kv => kv.Key.StartsWith(prefix)).ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            if (envValues.TryGetValue($"{prefix}{prop.Name}", out var value))
            {
                prop.SetValue(this, value);
            }
        }
        return this;
    }

    public IReadOnlyDictionary<string, string?> GetSettings()
    {
        var prefix = $"{Globals.AppName.CamelToPascalCase()}__";

        var envValues = EnvironmentVars.GetAll().Where(kv => kv.Key.StartsWith(prefix));

        Dictionary<string, string?> returnVal = [];

        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            returnVal[$"{prefix}{prop.Name}"] = prop.GetValue(this)?.ToString();
        }

        foreach (var kv in envValues)
        {
            returnVal[kv.Key] = kv.Value;
        }

        return returnVal;
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
        };
    }

    public AzureOpenAiOptions GetAzureOpenAIClientOptions()
    {
        return new AzureOpenAiOptions()
        {
            Endpoint = OpenAiEndpoint,
            ApiKey = OpenAiApiKey,
            DeploymentName = OpenAiDeploymentName,
        };
    }
}