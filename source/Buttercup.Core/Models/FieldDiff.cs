namespace Buttercup.Core.Models;

/// <summary>
/// Represents differences detected in a specific field of a content type
/// </summary>
public class FieldDiff
{
    /// <summary>
    /// The field identifier
    /// </summary>
    public string FieldId { get; set; } = string.Empty;

    /// <summary>
    /// The type of difference detected
    /// </summary>
    public DiffType Type { get; set; }

    /// <summary>
    /// The original field type (null for new fields)
    /// </summary>
    public string? OldType { get; set; }

    /// <summary>
    /// The new field type (null for deleted fields)
    /// </summary>
    public string? NewType { get; set; }

    /// <summary>
    /// Whether the type change is compatible (can be automatically converted)
    /// </summary>
    public bool IsTypeCompatible { get; set; } = true;

    /// <summary>
    /// Whether this field change requires data migration
    /// </summary>
    public bool RequiresDataMigration { get; set; }
}