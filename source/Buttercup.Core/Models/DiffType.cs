namespace Buttercup.Core.Models;

/// <summary>
/// Enum representing the type of difference detected
/// </summary>
public enum DiffType
{
    /// <summary>
    /// A new content type or field
    /// </summary>
    New,

    /// <summary>
    /// A modified content type or field
    /// </summary>
    Modified,

    /// <summary>
    /// A deleted content type or field
    /// </summary>
    Deleted
}