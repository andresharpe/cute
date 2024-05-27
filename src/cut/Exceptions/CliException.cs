namespace Cut.Exceptions;

[Serializable]
public class CliException: Exception, ICliException
{
    public CliException(string message): base(message)
    {
        
    }

    public CliException(string message, Exception innerException): base(message, innerException)
    {
        
    }
}