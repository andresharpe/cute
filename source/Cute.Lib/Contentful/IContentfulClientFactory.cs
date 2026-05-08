using Contentful.Core;
using Contentful.Core.Configuration;

namespace Cute.Lib.Contentful;

/// <summary>
/// Abstracts construction of the Contentful SDK clients so that <see cref="ContentfulConnection"/>
/// does not depend on the concrete <c>ContentfulClient</c> / <c>ContentfulManagementClient</c> types.
/// This makes it possible to substitute mock clients in unit tests.
/// </summary>
public interface IContentfulClientFactory
{
    /// <summary>
    /// Creates a delivery client (CDA) for the supplied options.
    /// </summary>
    IContentfulClient CreateDeliveryClient(ContentfulOptions options);

    /// <summary>
    /// Creates a preview client (CPA). Implementations must ensure
    /// <see cref="ContentfulOptions.UsePreviewApi"/> is set, regardless of the input.
    /// </summary>
    IContentfulClient CreatePreviewClient(ContentfulOptions options);

    /// <summary>
    /// Creates a management client (CMA) for the supplied options.
    /// </summary>
    IContentfulManagementClient CreateManagementClient(ContentfulOptions options);
}
