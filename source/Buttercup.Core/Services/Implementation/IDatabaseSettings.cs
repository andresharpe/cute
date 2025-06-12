namespace Buttercup.Core.Services.Implementation;

public interface IDatabaseSettings
{
    public string DatabaseProvider { get; set; }
    public string DatabaseConnectionString { get; set; }
}