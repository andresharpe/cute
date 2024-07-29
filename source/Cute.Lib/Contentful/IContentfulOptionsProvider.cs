using Contentful.Core.Configuration;

namespace Cute.Lib.Contentful;

public interface IContentfulOptionsProvider
{
    public ContentfulOptions GetContentfulOptions();
}