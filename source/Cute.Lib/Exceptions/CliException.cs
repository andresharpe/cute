namespace Cute.Lib.Exceptions;

[Serializable]
public class CliException : Exception, ICliException
{
    public CliException()
    {
    }

    public CliException(string message) : base(message)
    {
    }

    public CliException(string message, Exception innerException) : base(message, innerException)
    {
    }
}