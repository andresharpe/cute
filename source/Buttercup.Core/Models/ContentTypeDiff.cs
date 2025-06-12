namespace Buttercup.Core.Models;

/// <summary>
/// Represents differences detected between Contentful content types and local database schema
/// </summary>
public class ContentTypeDiff
{
    /// <summary>
    /// The content type identifier
    /// </summary>
    public string ContentTypeId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the content type
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of difference detected
    /// </summary>
    public DiffType Type { get; set; }

    /// <summary>
    /// List of all field changes in this content type
    /// </summary>
    public List<FieldDiff> FieldChanges { get; set; } = new();

    /// <summary>
    /// Whether any changes are breaking changes that could affect existing data
    /// </summary>
    public bool HasBreakingChanges { get; set; }
}