namespace Buttercup.Core.Models;

/// <summary>
/// Result of applying a schema change
/// </summary>
public class SchemaChangeResult
{
    /// <summary>
    /// The content type ID that was changed
    /// </summary>
    public string ContentTypeId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the change was successfully applied
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Message describing the result or error
    /// </summary>
    public string Message { get; set; } = string.Empty;
}