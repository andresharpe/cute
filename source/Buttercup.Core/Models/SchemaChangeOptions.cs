namespace Buttercup.Core.Models;

/// <summary>
/// Options for applying schema changes
/// </summary>
public class SchemaChangeOptions
{
    /// <summary>
    /// Whether to apply breaking changes
    /// </summary>
    public bool Force { get; set; }
}