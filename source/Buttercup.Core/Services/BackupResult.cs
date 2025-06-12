namespace Buttercup.Core.Services;

/// <summary>
/// Result of a database backup operation
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Whether the backup was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Path to the backup file if successful
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Error message if not successful
    /// </summary>
    public string Message { get; set; } = string.Empty;
}