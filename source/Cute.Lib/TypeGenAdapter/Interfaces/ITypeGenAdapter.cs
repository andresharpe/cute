using Contentful.Core.Models;

namespace Cute.Lib.TypeGenAdapter
{
    public interface ITypeGenAdapter
    {
        Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null);
    }
}