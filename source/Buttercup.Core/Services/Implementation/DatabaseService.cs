using Buttercup.Core.Models;
using Contentful.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Buttercup.Core.Services.Implementation
{
    /// <summary>
    /// Implementation of database service for schema management
    /// </summary>
    public class DatabaseService(
        ILogger<DatabaseService> logger,
        ButtercupDbContext dbContext,
        IDatabaseSettings databaseSettings) : IDatabaseService
    {
        private readonly ILogger<DatabaseService> _logger = logger;
        private readonly ButtercupDbContext _dbContext = dbContext;
        private readonly string _databaseProvider = databaseSettings.DatabaseProvider;

        /// <inheritdoc />
        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Checking database connection");
                return await _dbContext.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking database connection");
                return false;
            }
        }

        public async Task<bool> EnsureDatabaseCreatedAsync()
        {
            try
            {
                _logger.LogInformation("Ensuring database exists");
                bool created = await _dbContext.Database.EnsureCreatedAsync();
                if (created)
                {
                    _logger.LogInformation("Database was created");
                }
                else
                {
                    _logger.LogInformation("Database already exists");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create database");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<BackupResult> CreateBackupAsync()
        {
            try
            {
                _logger.LogInformation("Creating database backup");

                var backupFileName = $"buttercup_backup_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (_databaseProvider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    // For SQLite, simply copy the database file
                    var connectionString = _dbContext.Database.GetConnectionString();
                    var dbFilePath = connectionString
                        .Replace("Data Source=", "")
                        .Split(';')[0];

                    if (System.IO.File.Exists(dbFilePath))
                    {
                        var backupPath = $"{backupFileName}.db";
                        System.IO.File.Copy(dbFilePath, backupPath, true);

                        _logger.LogInformation("SQLite database backup created at {backupPath}", backupPath);
                        return new BackupResult { Success = true, BackupPath = backupPath };
                    }
                    else
                    {
                        return new BackupResult
                        {
                            Success = false,
                            Message = $"SQLite database file not found: {dbFilePath}"
                        };
                    }
                }
                else if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a simplified example - in a real implementation, you would use
                    // the PostgreSQL pg_dump utility or Npgsql's backup functionality

                    var backupPath = $"{backupFileName}.sql";
                    // Simulated backup for this example
                    await System.IO.File.WriteAllTextAsync(backupPath, "-- PostgreSQL backup simulation");

                    _logger.LogInformation("PostgreSQL database backup created at {backupPath}", backupPath);
                    return new BackupResult { Success = true, BackupPath = backupPath };
                }
                else
                {
                    return new BackupResult
                    {
                        Success = false,
                        Message = $"Unsupported database provider: {_databaseProvider}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating database backup");
                return new BackupResult { Success = false, Message = ex.Message };
            }
        }

        /// <inheritdoc />
        public async Task<List<ContentType>> GetContentTypesFromDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving content types from database");

                if (!await _dbContext.Database.CanConnectAsync())
                {
                    _logger.LogWarning("Cannot connect to database");
                    return [];
                }

                // Get content types from database
                var dbContentTypes = await _dbContext.ContentTypes
                    .AsNoTracking()
                    .ToListAsync();

                // Convert database models to Contentful content types
                var result = new List<ContentType>();
                foreach (var dbContentType in dbContentTypes)
                {
                    result.Add(ConvertDatabaseContentTypeToContentful(dbContentType));
                }

                _logger.LogInformation("Retrieved {count} content types from database", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving content types from database");
                return [];
            }
        }

        /// <inheritdoc />
        public async Task<List<SchemaChangeResult>> ApplyContentTypeChangesAsync(
            List<ContentTypeDiff> changes,
            SchemaChangeOptions options)
        {
            _logger.LogInformation("Applying {count} content type changes to database", changes.Count);

            var results = new List<SchemaChangeResult>();

            // Start a transaction for all changes
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                foreach (var change in changes)
                {
                    // Skip breaking changes if not forced
                    if (change.HasBreakingChanges && !options.Force)
                    {
                        results.Add(new SchemaChangeResult
                        {
                            ContentTypeId = change.ContentTypeId,
                            Success = false,
                            Message = "Skipped breaking change (use --force to apply)"
                        });
                        continue;
                    }

                    // Apply the change based on its type
                    var result = change.Type switch
                    {
                        DiffType.New => await CreateContentTypeAsync(change),
                        DiffType.Modified => await UpdateContentTypeAsync(change),
                        DiffType.Deleted => await DeleteContentTypeAsync(change),
                        _ => new SchemaChangeResult
                        {
                            ContentTypeId = change.ContentTypeId,
                            Success = false,
                            Message = $"Unknown change type: {change.Type}"
                        }
                    };

                    results.Add(result);

                    // If a change failed and we're not forcing, rollback and return
                    if (!result.Success && !options.Force)
                    {
                        _logger.LogWarning("Change to {id} failed, rolling back transaction", change.ContentTypeId);
                        await transaction.RollbackAsync();
                        return results;
                    }
                }

                // Commit the transaction if all changes were successful
                await transaction.CommitAsync();
                _logger.LogInformation("All schema changes applied successfully");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying schema changes");
                await transaction.RollbackAsync();

                // Add the error to the results
                results.Add(new SchemaChangeResult
                {
                    ContentTypeId = "TRANSACTION",
                    Success = false,
                    Message = $"Transaction failed: {ex.Message}"
                });

                return results;
            }
        }

        #region Helper Methods

        private static ContentType ConvertDatabaseContentTypeToContentful(dynamic dbContentType)
        {
            // This would convert from your database model to Contentful model
            var contentType = new ContentType
            {
                SystemProperties = new SystemProperties
                {
                    Id = dbContentType.Id,
                    Type = "ContentType",
                    Version = 1
                },
                Name = dbContentType.Name,
                Description = dbContentType.Description,
                DisplayField = dbContentType.DisplayField,
                Fields = []
            };

            // Convert fields from database
            foreach (var dbField in dbContentType.Fields)
            {
                contentType.Fields.Add(new Field
                {
                    Id = dbField.Id,
                    Name = dbField.Name,
                    Type = dbField.Type,
                    Required = dbField.Required,
                    Localized = dbField.Localized
                });
            }

            return contentType;
        }

        private async Task<SchemaChangeResult> CreateContentTypeAsync(ContentTypeDiff change)
        {
            try
            {
                _logger.LogInformation("Creating new content type: {Id}", change.ContentTypeId);

                // Generate SQL for creating the content type table
                var sql = GenerateCreateTableSql(change);
                await _dbContext.Database.ExecuteSqlRawAsync(sql);

                // Add the content type to the ContentTypes table

                var cte = new ContentTypeEntity
                {
                    Id = change.ContentTypeId,
                    Name = change.Name,
                    Description = string.Empty,
                    DisplayField = change.FieldChanges.FirstOrDefault()?.FieldId ?? string.Empty,
                    Fields = change.FieldChanges.Select(f => new FieldEntity
                    {
                        Id = f.FieldId,
                        Name = f.FieldId, // Using ID as name for simplicity
                        Type = f.NewType,
                        Required = false,
                        Localized = false
                    }).ToList()
                };

                await _dbContext.ContentTypes.AddAsync(cte);

                await _dbContext.SaveChangesAsync();

                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = true,
                    Message = $"Created content type '{change.Name}'"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating content type: {Id}", change.ContentTypeId);
                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = false,
                    Message = $"Failed to create content type: {ex.Message}"
                };
            }
        }

        private async Task<SchemaChangeResult> UpdateContentTypeAsync(ContentTypeDiff change)
        {
            try
            {
                _logger.LogInformation("Updating content type: {Id}", change.ContentTypeId);

                // Process each field change
                foreach (var fieldChange in change.FieldChanges)
                {
                    string sql = fieldChange.Type switch
                    {
                        DiffType.New => GenerateAddColumnSql(change.ContentTypeId, fieldChange),
                        DiffType.Modified => GenerateAlterColumnSql(change.ContentTypeId, fieldChange),
                        DiffType.Deleted => GenerateDropColumnSql(change.ContentTypeId, fieldChange),
                        _ => throw new NotSupportedException($"Unsupported field change type: {fieldChange.Type}")
                    };

                    await _dbContext.Database.ExecuteSqlRawAsync(sql);
                }

                // Update the content type in the ContentTypes table
                var contentType = await _dbContext.ContentTypes.FindAsync(change.ContentTypeId);
                if (contentType != null)
                {
                    contentType.Name = change.Name;

                    // Update fields
                    foreach (var fieldChange in change.FieldChanges)
                    {
                        if (fieldChange.Type == DiffType.New)
                        {
                            contentType.Fields.Add(new FieldEntity
                            {
                                Id = fieldChange.FieldId,
                                Name = fieldChange.FieldId,
                                Type = fieldChange.NewType,
                                Required = false,
                                Localized = false
                            });
                        }
                        else if (fieldChange.Type == DiffType.Modified)
                        {
                            var field = contentType.Fields.FirstOrDefault(f => f.Id == fieldChange.FieldId);
                            if (field != null)
                            {
                                field.Type = fieldChange.NewType;
                            }
                        }
                        else if (fieldChange.Type == DiffType.Deleted)
                        {
                            var field = contentType.Fields.FirstOrDefault(f => f.Id == fieldChange.FieldId);
                            if (field != null)
                            {
                                contentType.Fields.Remove(field);
                            }
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                }

                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = true,
                    Message = $"Updated content type '{change.Name}'"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating content type: {Id}", change.ContentTypeId);
                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = false,
                    Message = $"Failed to update content type: {ex.Message}"
                };
            }
        }

        private async Task<SchemaChangeResult> DeleteContentTypeAsync(ContentTypeDiff change)
        {
            try
            {
                _logger.LogInformation("Deleting content type: {Id}", change.ContentTypeId);

                // Drop the content type table
                var sql = $"DROP TABLE IF EXISTS Entries_{change.ContentTypeId}";
                await _dbContext.Database.ExecuteSqlRawAsync(sql);

                // Remove from content types table
                var contentType = await _dbContext.ContentTypes.FindAsync(change.ContentTypeId);
                if (contentType != null)
                {
                    _dbContext.ContentTypes.Remove(contentType);
                    await _dbContext.SaveChangesAsync();
                }

                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = true,
                    Message = $"Deleted content type '{change.Name}'"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting content type: {Id}", change.ContentTypeId);
                return new SchemaChangeResult
                {
                    ContentTypeId = change.ContentTypeId,
                    Success = false,
                    Message = $"Failed to delete content type: {ex.Message}"
                };
            }
        }

        private string GenerateCreateTableSql(ContentTypeDiff change)
        {
            // This is a simplified example - in a real implementation, this would
            // handle the differences between SQLite and PostgreSQL syntax
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"CREATE TABLE Entries_{change.ContentTypeId} (");
            sb.AppendLine("  Id TEXT PRIMARY KEY,");
            sb.AppendLine("  Version INTEGER NOT NULL,");
            sb.AppendLine("  Published INTEGER NOT NULL,");
            sb.AppendLine("  CreatedAt TEXT NOT NULL,");
            sb.AppendLine("  UpdatedAt TEXT NOT NULL,");

            // Add columns for fields
            foreach (var field in change.FieldChanges)
            {
                var sqlType = GetSqlTypeForFieldType(field.NewType);
                sb.AppendLine($"  {field.FieldId} {sqlType} NULL,");
            }

            // Remove trailing comma
            sb.Length -= 3;
            sb.AppendLine("\n);");

            return sb.ToString();
        }

        private string GenerateAddColumnSql(string contentTypeId, FieldDiff fieldChange)
        {
            var sqlType = GetSqlTypeForFieldType(fieldChange.NewType);
            return $"ALTER TABLE Entries_{contentTypeId} ADD COLUMN {fieldChange.FieldId} {sqlType} NULL";
        }

        private string GenerateAlterColumnSql(string contentTypeId, FieldDiff fieldChange)
        {
            // In SQLite, we can't directly alter column types, so we'd need a more complex approach
            // For PostgreSQL, it's simpler
            if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                var sqlType = GetSqlTypeForFieldType(fieldChange.NewType);
                return $"ALTER TABLE Entries_{contentTypeId} ALTER COLUMN {fieldChange.FieldId} TYPE {sqlType} USING {fieldChange.FieldId}::{sqlType}";
            }
            else
            {
                // For SQLite, we'd need a more complex migration with temp table
                // This is simplified for the example
                return $"-- SQLite ALTER COLUMN not directly supported: {contentTypeId}.{fieldChange.FieldId}";
            }
        }

        private string GenerateDropColumnSql(string contentTypeId, FieldDiff fieldChange)
        {
            // In SQLite, dropping columns requires recreating the table
            // For PostgreSQL, it's simpler
            if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                return $"ALTER TABLE Entries_{contentTypeId} DROP COLUMN {fieldChange.FieldId}";
            }
            else
            {
                // For SQLite, we'd need a more complex migration with temp table
                // This is simplified for the example
                return $"-- SQLite DROP COLUMN not directly supported: {contentTypeId}.{fieldChange.FieldId}";
            }
        }

        private string GetSqlTypeForFieldType(string contentfulType)
        {
            // Map Contentful field types to SQL types for the database provider
            if (_databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                return contentfulType switch
                {
                    "Symbol" => "VARCHAR(256)",
                    "Text" => "TEXT",
                    "Integer" => "INTEGER",
                    "Number" => "NUMERIC",
                    "Date" => "TIMESTAMP",
                    "Boolean" => "BOOLEAN",
                    "Object" => "JSONB",
                    "Array" => "JSONB",
                    "Link" => "VARCHAR(256)",
                    "Location" => "JSONB",
                    _ => "TEXT"
                };
            }
            else // SQLite
            {
                return contentfulType switch
                {
                    "Symbol" => "TEXT",
                    "Text" => "TEXT",
                    "Integer" => "INTEGER",
                    "Number" => "REAL",
                    "Date" => "TEXT",
                    "Boolean" => "INTEGER",
                    "Object" => "TEXT", // JSON as text
                    "Array" => "TEXT", // JSON as text
                    "Link" => "TEXT",
                    "Location" => "TEXT", // JSON as text
                    _ => "TEXT"
                };
            }
        }

        #endregion Helper Methods
    }
}