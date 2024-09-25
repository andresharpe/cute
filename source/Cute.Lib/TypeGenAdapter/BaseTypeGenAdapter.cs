using Contentful.Core.Models;
using Cute.Lib.Contentful.BulkActions;
using File = System.IO.File;

namespace Cute.Lib.TypeGenAdapter
{
    public abstract class BaseTypeGenAdapter(DisplayActions displayActions) : ITypeGenAdapter
    {
        private readonly DisplayActions _displayActions = displayActions;

        public abstract Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null);

        public abstract Task PostGenerateTypeSource();

        public abstract Task PreGenerateTypeSource(List<ContentType> contentTypes, string path, string? fileName = null, string? namespc = null);

        protected bool WarnIfFileExists(string path)
        {
            if (File.Exists(path) && _displayActions.ConfirmWithPromptChallenge != null)
            {
                return _displayActions.ConfirmWithPromptChallenge($"overwrite {path}");
            }
            return true;
        }
    }
}