using Cute.Lib.Exceptions;

namespace Cute.Services.Translation
{
    /// <summary>
    /// Thrown when a translation response was cut short by the model's output token limit and nothing
    /// usable could be parsed out of it. The request was still made and billed, so this is reported
    /// rather than silently returning "no translation".
    /// </summary>
    [Serializable]
    public class TranslationTruncatedException : Exception, ICliException
    {
        public TranslationTruncatedException()
        {
        }

        public TranslationTruncatedException(string message) : base(message)
        {
        }

        public TranslationTruncatedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
