using dotenv.net;
using System.Collections;

namespace Cute.Lib.Config;

public class EnvironmentVars
{
    private static readonly Dictionary<string, string> _env;

    private static readonly EnvironmentVars _instance = new();

    static EnvironmentVars()
    {
        _env = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Where(e => e.Value is not null)
            .ToDictionary(e => (string)e.Key, e => e.Value?.ToString() ?? string.Empty);

        foreach (var (key, value) in DotEnv.Fluent().Read())
        {
            _env[key] = value;
        }
    }

    public static EnvironmentVars Instance => _instance;

    public string? this[string variable]
    {
        get => _env.TryGetValue(variable, out var value) ? value : null;
    }

    public static IReadOnlyDictionary<string, string> GetAll()
    {
        return _env;
    }
}