using Contentful.Core.Models;

namespace Cute.Lib.TypeGenAdapter
{
    public interface ITypeGenAdapter
    {
        Task PreGenerateTypeSource(List<ContentType> contentTypes, string path, string? fileName = null, string? namespc = null);

        Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null);

        Task PostGenerateTypeSource();
    }
}