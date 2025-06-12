using Buttercup.Core.Models;
using Buttercup.Core.Services;
using Contentful.Core.Models;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Buttercup
{
    /// <summary>
    /// Command to synchronize the schema between Contentful and Buttercup database.
    /// Uses --force flag (inherited from LoggedInSettings) to bypass prompts for unattended script execution.
    /// </summary>
    public class ButtercupSyncSchemaCommand : BaseLoggedInCommand<ButtercupSyncSchemaCommand.Settings>
    {
        private readonly IDatabaseService _databaseService;

        public ButtercupSyncSchemaCommand(
            IConsoleWriter console,
            ILogger<ButtercupSyncSchemaCommand> logger,
            AppSettings appSettings,
            IDatabaseService databaseService)
            : base(console, logger, appSettings)
        {
            _databaseService = databaseService;
        }

        public class Settings : LoggedInSettings
        {
            [CommandOption("-a|--apply")]
            [Description("Apply schema changes. If not specified then the plan will be displayed")]
            public bool Apply { get; set; }

            [CommandOption("-b|--backup")]
            [Description("Create a database backup before applying changes")]
            public bool Backup { get; set; }
        }

        public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
        {
            _console.WriteNormal("Analyzing content type changes...");

            // 1. Verify and ensure database exists
            bool dbCreated = await _databaseService.EnsureDatabaseCreatedAsync();
            if (!dbCreated)
            {
                _console.WriteAlert("Failed to create database. Check your configuration.");
                _console.WriteNormal("Run 'cute buttercup configure' to set up your database connection.");
                return 1;
            }

            // Verify database connection
            if (!await _databaseService.CheckConnectionAsync())
            {
                _console.WriteAlert("Database connection failed. Check your configuration.");
                _console.WriteNormal("Run 'cute buttercup configure' to set up your database connection.");
                return 1;
            }

            // 2. Fetch content types from Contentful using the existing ContentfulConnection
            var contentfulTypes = (await ContentfulConnection.GetContentTypesAsync())
                .OrderBy(ct => ct.Name)
                .ToList();

            // 3. Fetch content types from database
            var databaseTypes = await _databaseService.GetContentTypesFromDatabaseAsync();

            // 4. Analyze differences
            var changes = AnalyzeContentTypeChanges(contentfulTypes, databaseTypes);

            // 5. Display summary of changes
            DisplayChangeSummary(changes);

            // 6. If no changes detected, exit early
            if (changes.Count == 0)
            {
                _console.WriteNormal("No schema changes detected. Database schema is up to date.", Globals.StyleNormal);
                return 0;
            }

            // 7. If not applying changes, just exit after displaying
            if (!settings.Apply)
            {
                _console.WriteNormal("\nTo apply these changes, run the command with --apply flag.");
                return 0;
            }

            // 8. Check for breaking changes
            var hasBreakingChanges = changes.Any(c => c.HasBreakingChanges);

            // 9. If breaking changes exist and not forcing, prompt user
            if (hasBreakingChanges && !settings.Force)
            {
                return await HandleBreakingChanges(changes, settings);
            }
            else if (hasBreakingChanges && settings.Force)
            {
                _console.WriteAlert("Breaking changes detected. Applying all changes due to --force flag.");
            }

            // 10. Apply changes
            return await ApplySchemaChanges(changes, settings);
        }

        private List<ContentTypeDiff> AnalyzeContentTypeChanges(
            List<ContentType> contentfulTypes,
            List<ContentType> databaseTypes)
        {
            _logger.LogInformation("Analyzing content type changes");

            var result = new List<ContentTypeDiff>();

            // Find new content types (in Contentful but not in database)
            foreach (var contentfulType in contentfulTypes)
            {
                var contentTypeId = contentfulType.SystemProperties.Id;
                var databaseType = databaseTypes.FirstOrDefault(dt =>
                    dt.SystemProperties.Id == contentTypeId);

                if (databaseType == null)
                {
                    // This is a new content type
                    result.Add(CreateNewContentTypeDiff(contentfulType));
                }
                else
                {
                    // This is an existing content type, check for changes
                    var diff = CompareContentTypes(contentfulType, databaseType);
                    if (diff.FieldChanges.Count != 0)
                    {
                        result.Add(diff);
                    }
                }
            }

            // Find deleted content types (in database but not in Contentful)
            foreach (var databaseType in databaseTypes)
            {
                var contentTypeId = databaseType.SystemProperties.Id;
                if (!contentfulTypes.Any(ct => ct.SystemProperties.Id == contentTypeId))
                {
                    // This is a deleted content type
                    result.Add(CreateDeletedContentTypeDiff(databaseType));
                }
            }

            _logger.LogInformation("Found {count} content types with changes", result.Count);
            return result;
        }

        private static ContentTypeDiff CreateNewContentTypeDiff(ContentType contentType)
        {
            var diff = new ContentTypeDiff
            {
                ContentTypeId = contentType.SystemProperties.Id,
                Name = contentType.Name,
                Type = DiffType.New,
                HasBreakingChanges = false // New types don't break existing data
            };

            // All fields in a new content type are new
            foreach (var field in contentType.Fields)
            {
                diff.FieldChanges.Add(new FieldDiff
                {
                    FieldId = field.Id,
                    Type = DiffType.New,
                    NewType = field.Type,
                    IsTypeCompatible = true,
                    RequiresDataMigration = false
                });
            }

            return diff;
        }

        private static ContentTypeDiff CreateDeletedContentTypeDiff(ContentType contentType)
        {
            var diff = new ContentTypeDiff
            {
                ContentTypeId = contentType.SystemProperties.Id,
                Name = contentType.Name,
                Type = DiffType.Deleted,
                HasBreakingChanges = true // Deleting is always breaking
            };

            // All fields in a deleted content type are deleted
            foreach (var field in contentType.Fields)
            {
                diff.FieldChanges.Add(new FieldDiff
                {
                    FieldId = field.Id,
                    Type = DiffType.Deleted,
                    OldType = field.Type,
                    IsTypeCompatible = true,
                    RequiresDataMigration = true
                });
            }

            return diff;
        }

        private static ContentTypeDiff CompareContentTypes(ContentType contentfulType, ContentType databaseType)
        {
            var diff = new ContentTypeDiff
            {
                ContentTypeId = contentfulType.SystemProperties.Id,
                Name = contentfulType.Name,
                Type = DiffType.Modified,
                HasBreakingChanges = false
            };

            // Compare fields
            // 1. Find added fields (in Contentful but not in database)
            foreach (var contentfulField in contentfulType.Fields)
            {
                var databaseField = databaseType.Fields.FirstOrDefault(f => f.Id == contentfulField.Id);

                if (databaseField == null)
                {
                    // New field
                    diff.FieldChanges.Add(new FieldDiff
                    {
                        FieldId = contentfulField.Id,
                        Type = DiffType.New,
                        NewType = contentfulField.Type,
                        IsTypeCompatible = true,
                        RequiresDataMigration = false
                    });
                }
                else if (!FieldsAreEqual(contentfulField, databaseField))
                {
                    // Modified field
                    var fieldDiff = new FieldDiff
                    {
                        FieldId = contentfulField.Id,
                        Type = DiffType.Modified,
                        OldType = databaseField.Type,
                        NewType = contentfulField.Type,
                        IsTypeCompatible = IsTypeConversionCompatible(databaseField.Type, contentfulField.Type),
                        RequiresDataMigration = databaseField.Type != contentfulField.Type
                    };

                    // Check if this is a breaking change
                    if (!fieldDiff.IsTypeCompatible || (databaseField.Required == false && contentfulField.Required == true))
                    {
                        diff.HasBreakingChanges = true;
                    }

                    diff.FieldChanges.Add(fieldDiff);
                }
            }

            // 2. Find removed fields (in database but not in Contentful)
            foreach (var databaseField in databaseType.Fields)
            {
                if (!contentfulType.Fields.Any(f => f.Id == databaseField.Id))
                {
                    // Deleted field
                    diff.FieldChanges.Add(new FieldDiff
                    {
                        FieldId = databaseField.Id,
                        Type = DiffType.Deleted,
                        OldType = databaseField.Type,
                        IsTypeCompatible = true,
                        RequiresDataMigration = true
                    });

                    // Deleting a field is always a breaking change
                    diff.HasBreakingChanges = true;
                }
            }

            return diff;
        }

        private static bool FieldsAreEqual(Field field1, Field field2)
        {
            // Compare all important field properties
            return field1.Id == field2.Id &&
                   field1.Name == field2.Name &&
                   field1.Type == field2.Type &&
                   field1.Required == field2.Required &&
                   field1.Localized == field2.Localized;
        }

        private static bool IsTypeConversionCompatible(string oldType, string newType)
        {
            // This determines if a field type conversion is data-compatible
            if (oldType == newType)
                return true;

            // Some common compatible conversions
            if (oldType == "Symbol" && newType == "Text")
                return true;

            if (oldType == "Integer" && newType == "Number")
                return true;

            // Most other type conversions are potentially breaking
            return false;
        }

        private void DisplayChangeSummary(List<ContentTypeDiff> changes)
        {
            if (changes.Count == 0)
                return;

            _console.WriteNormal($"\nChanges detected in {changes.Count} content types:");

            // Group changes by type
            var newTypes = changes.Where(c => c.Type == DiffType.New).ToList();
            var modifiedTypes = changes.Where(c => c.Type == DiffType.Modified).ToList();
            var deletedTypes = changes.Where(c => c.Type == DiffType.Deleted).ToList();

            // Display new content types
            foreach (var newType in newTypes)
            {
                _console.WriteNormal($" - <NEW> '{newType.ContentTypeId}': New content type with {newType.FieldChanges.Count} fields", Globals.StyleAlert);
            }

            // Display modified content types
            foreach (var modifiedType in modifiedTypes)
            {
                _console.WriteNormal($" - <MODIFIED> '{modifiedType.ContentTypeId}':", Globals.StyleAlertAccent);

                foreach (var fieldChange in modifiedType.FieldChanges)
                {
                    var changeDesc = fieldChange.Type switch
                    {
                        DiffType.New => $"<ADDED> '{fieldChange.FieldId}' field ({fieldChange.NewType})",
                        DiffType.Modified => fieldChange.OldType != fieldChange.NewType
                            ? $"<MODIFIED> '{fieldChange.FieldId}' field ({fieldChange.OldType} → {fieldChange.NewType})"
                            : $"<MODIFIED> '{fieldChange.FieldId}' field (validation changes)",
                        DiffType.Deleted => $"<REMOVED> '{fieldChange.FieldId}' field",
                        _ => $"Unknown change to '{fieldChange.FieldId}' field"
                    };

                    var style = fieldChange.Type == DiffType.New
                        ? Globals.StyleNormal
                        : fieldChange.Type == DiffType.Deleted
                            ? Globals.StyleAlert
                            : Globals.StyleAlertAccent;

                    _console.WriteNormal($"   - {changeDesc}", style);
                }
            }

            // Display deleted content types
            foreach (var deletedType in deletedTypes)
            {
                _console.WriteNormal($" - <DELETED> '{deletedType.ContentTypeId}': Content type will be removed", Globals.StyleAlert);
            }

            // Check for breaking changes
            var breakingChanges = changes.Where(c => c.HasBreakingChanges).ToList();
            if (breakingChanges.Count != 0)
            {
                _console.WriteAlert("\nWARNING: The following changes may affect existing content:");

                foreach (var change in breakingChanges)
                {
                    if (change.Type == DiffType.Deleted)
                    {
                        _console.WriteNormal($" - '{change.ContentTypeId}': Deletion will remove all entries", Globals.StyleAlert);
                        continue;
                    }

                    var breakingFieldChanges = change.FieldChanges.Where(f => f.RequiresDataMigration).ToList();
                    foreach (var fieldChange in breakingFieldChanges)
                    {
                        if (fieldChange.Type == DiffType.Modified && !fieldChange.IsTypeCompatible)
                        {
                            _console.WriteNormal($" - '{change.ContentTypeId}.{fieldChange.FieldId}': Type change from {fieldChange.OldType} to {fieldChange.NewType}", Globals.StyleAlertAccent);
                            _console.WriteNormal($"   Entries may have values that cannot be converted automatically", Globals.StyleAlertAccent);
                        }
                        else if (fieldChange.Type == DiffType.Deleted)
                        {
                            _console.WriteNormal($" - '{change.ContentTypeId}.{fieldChange.FieldId}': Field will be removed", Globals.StyleAlertAccent);
                        }
                    }
                }
            }
        }

        private async Task<int> HandleBreakingChanges(List<ContentTypeDiff> changes, Settings settings)
        {
            var prompt = new SelectionPrompt<string>()
                .Title("Breaking changes detected. Do you want to:")
                .AddChoices(
                [
                    "Apply all changes (data loss possible)",
                    "Apply non-breaking changes only",
                    "Cancel"
                ]);

            var choice = _console.Prompt(prompt);

            switch (choice)
            {
                case "Apply all changes (data loss possible)":
                    // Create a new settings object with Force set to true
                    var forceSettings = new Settings
                    {
                        Apply = settings.Apply,
                        Backup = settings.Backup,
                        Force = true,
                        SpaceId = settings.SpaceId,
                        EnvironmentId = settings.EnvironmentId,
                        ManagementToken = settings.ManagementToken,
                        ContentDeliveryToken = settings.ContentDeliveryToken,
                        ContentPreviewToken = settings.ContentPreviewToken
                    };
                    return await ApplySchemaChanges(changes, forceSettings);

                case "Apply non-breaking changes only":
                    var nonBreakingChanges = changes.Where(c => !c.HasBreakingChanges).ToList();
                    return await ApplySchemaChanges(nonBreakingChanges, settings);

                case "Cancel":
                default:
                    _console.WriteNormal("Schema synchronization cancelled.");
                    return 0;
            }
        }

        private async Task<int> ApplySchemaChanges(List<ContentTypeDiff> changes, Settings settings)
        {
            _console.WriteNormal("Applying schema changes...");

            // Create backup if requested
            if (settings.Backup)
            {
                _console.WriteNormal("Creating database backup...");
                var backupResult = await _databaseService.CreateBackupAsync();

                if (!backupResult.Success)
                {
                    _console.WriteAlert($"Database backup failed: {backupResult.Message}");
                    if (!settings.Force)
                    {
                        _console.WriteNormal("Use --force to continue without backup.");
                        return 1;
                    }
                    else
                    {
                        _console.WriteAlert("Continuing without backup due to --force flag.");
                    }
                }
                else
                {
                    _console.WriteNormal($"Database backup created at: {backupResult.BackupPath}", Globals.StyleNormal);
                }
            }

            // Apply the changes
            try
            {
                var results = await _databaseService.ApplyContentTypeChangesAsync(changes,
                    new SchemaChangeOptions { Force = settings.Force });

                // Show applied changes summary
                var applied = results.Count(r => r.Success);
                var failed = results.Count(r => !r.Success);

                _console.WriteNormal("Schema synchronization completed.", Globals.StyleNormal);
                _console.WriteNormal($"Successfully applied {applied} changes.");

                if (failed > 0)
                {
                    _console.WriteAlert($"Failed to apply {failed} changes:");

                    // List failures
                    foreach (var failure in results.Where(r => !r.Success))
                    {
                        _console.WriteNormal($" - {failure.ContentTypeId}: {failure.Message}", Globals.StyleAlert);
                    }
                }

                return failed > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                _console.WriteAlert($"Schema synchronization failed: {ex.Message}");
                _logger.LogError(ex, "Schema synchronization failed");

                if (settings.Backup)
                {
                    _console.WriteNormal("You can restore from the backup that was created before migration.");
                }

                return 1;
            }
        }
    }
}