using Contentful.Core.Models;
using File = System.IO.File;

namespace Cute.Lib.TypeGenAdapter
{
    public abstract class BaseTypeGenAdapter(Func<FormattableString, bool> fileExistsWarningChallenge) : ITypeGenAdapter
    {
        private readonly Func<FormattableString, bool> _fileExistsWarningChallenge = fileExistsWarningChallenge;

        public abstract Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null);

        public abstract Task PostGenerateTypeSource();

        public abstract Task PreGenerateTypeSource(List<ContentType> contentTypes, string path, string? fileName = null, string? namespc = null);

        protected bool WarnIfFileExists(string path)
        {
            if (File.Exists(path))
            {
                return fileExistsWarningChallenge($"overwrite {path}");
            }
            return true;
        }
    }
}