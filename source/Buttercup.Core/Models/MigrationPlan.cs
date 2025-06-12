namespace Buttercup.Core.Models;

/// <summary>
/// Represents a migration plan that can be saved to a file
/// </summary>
public class MigrationPlan
{
    /// <summary>
    /// When the migration plan was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// List of content type changes to be applied
    /// </summary>
    public List<ContentTypeDiff> Changes { get; set; } = new();
}