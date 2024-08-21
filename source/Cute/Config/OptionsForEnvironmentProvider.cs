using Contentful.Core.Configuration;
using Cute.Lib.Contentful;

namespace Cute.Config;

public class OptionsForEnvironmentProvider : IContentfulOptionsProvider
{
    private readonly IContentfulOptionsProvider _appSettings;

    private readonly string _environment;

    public OptionsForEnvironmentProvider(IContentfulOptionsProvider appSettings, string environment)
    {
        _appSettings = appSettings;
        _environment = environment;
    }

    public ContentfulOptions GetContentfulOptions()
    {
        var defaultOptions = _appSettings.GetContentfulOptions();

        defaultOptions.Environment = _environment;

        return defaultOptions;
    }
}