using Buttercup.Core.Models;
using Contentful.Core.Models;

namespace Buttercup.Core.Services;

/// <summary>
/// Service for database operations related to schema management
/// </summary>
public interface IDatabaseService
{
    /// <summary>
    /// Checks if the database connection is valid
    /// </summary>
    /// <returns>True if the connection is valid</returns>
    Task<bool> CheckConnectionAsync();

    /// <summary>
    /// Creates a backup of the database
    /// </summary>
    /// <returns>Result of the backup operation</returns>
    Task<BackupResult> CreateBackupAsync();

    /// <summary>
    /// Retrieves content types from the local database
    /// </summary>
    /// <returns>List of content types from the database</returns>
    Task<List<ContentType>> GetContentTypesFromDatabaseAsync();

    /// <summary>
    /// Applies content type changes to the database
    /// </summary>
    /// <param name="changes">List of content type changes to apply</param>
    /// <param name="options">Options for applying changes</param>
    /// <returns>Results of applying each change</returns>
    Task<List<SchemaChangeResult>> ApplyContentTypeChangesAsync(
        List<ContentTypeDiff> changes,
        SchemaChangeOptions options);

    Task<bool> EnsureDatabaseCreatedAsync();
}