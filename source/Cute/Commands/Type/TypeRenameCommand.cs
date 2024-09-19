using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Contentful.Core.Search;
using Cute.Commands.BaseCommands;
using Cute.Commands.Login;
using Cute.Config;
using Cute.Constants;
using Cute.Lib.Contentful;
using Cute.Lib.Contentful.BulkActions.Actions;
using Cute.Lib.Exceptions;
using Cute.Lib.Extensions;
using Cute.Services;
using Newtonsoft.Json.Linq;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Cute.Commands.Type;

public class TypeRenameCommand(IConsoleWriter console, ILogger<TypeRenameCommand> logger, ContentfulConnection contentfulConnection,
    AppSettings appSettings, HttpClient httpClient) : BaseLoggedInCommand<TypeRenameCommand.Settings>(console, logger, contentfulConnection, appSettings)
{
    private readonly HttpClient _httpClient = httpClient;
    public class Settings : LoggedInSettings
    {
        [CommandOption("-c|--content-type")]
        [Description("Specifies the content type to generate types for. Default is all.")]
        public string ContentTypeId { get; set; } = null!;

        [CommandOption("-p|--publish")]
        [Description("Whether to publish the created content or not. Useful if no circular references exist.")]
        public bool Publish { get; set; } = false;

        [CommandOption("-n|--new-id")]
        [Description("The id to rename the content type to.")]
        public string NewContentId { get; set; } = string.Empty;

        [CommandOption("-a|--apply-naming-convention")]
        [Description("The id to rename the content type to.")]
        public bool ApplyNamingConvention { get; set; } = false;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings.ApplyNamingConvention)
        {
            return base.Validate(context, settings);
        }

        if (string.IsNullOrEmpty(settings.NewContentId))
        {
            return ValidationResult.Error($"No new id specified..");
        }

        if (settings.NewContentId.Equals(settings.ContentTypeId))
        {
            return ValidationResult.Error($"The old and the new id must be different.");
        }

        return base.Validate(context, settings);
    }
    public override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        if (settings.ApplyNamingConvention)
        {
            return await CheckAndApplyNamingConventions();
        }

        string oldContentTypeId = settings.ContentTypeId!;
        string newContentTypeId = settings.NewContentId!;

        ContentType? contentTypeOld = null;
        ContentType? contentTypeNew = null;

        try
        {
            contentTypeNew = GetContentTypeOrThrowError(newContentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{newContentTypeId} found in environment {ContentfulEnvironmentId}", Globals.StyleHeading);
            if (!ConfirmWithPromptChallenge($"potentially destroy all '{oldContentTypeId}' entries in {ContentfulEnvironmentId}"))
            {
                return -1;
            }
        }
        catch
        {
            _console.WriteNormalWithHighlights($"The content type {newContentTypeId} does not exist in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }

        try
        {
            contentTypeOld = GetContentTypeOrThrowError(oldContentTypeId);
            _console.WriteBlankLine();
            _console.WriteNormalWithHighlights($"{oldContentTypeId} found in environment {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        catch (Exception ex)
        {
            throw new CliException(ex.Message);
        }

        _console.WriteBlankLine();

        var bulkActions = new List<IBulkAction>();

        if (contentTypeNew is null)
        {
            contentTypeNew = await contentTypeOld.CloneWithId(ContentfulManagementClient, newContentTypeId);            
            _console.WriteNormalWithHighlights($"Success. Created {newContentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }
        else
        {
            if (!ConfirmWithPromptChallenge($"destroy all '{newContentTypeId}' entries in {ContentfulEnvironmentId}"))
            {
                return -1;
            }

            await PerformBulkOperations(
                [
                    new DeleteBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentTypeNew)
                    .WithContentLocales(ContentLocales)
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                ]
            );

            await ContentfulManagementClient.DeactivateContentType(newContentTypeId);

            await ContentfulManagementClient.DeleteContentType(newContentTypeId);

            _console.WriteNormalWithHighlights($"Deleted {newContentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);

            contentTypeNew = await contentTypeOld.CloneWithId(ContentfulManagementClient, newContentTypeId);

            _console.WriteNormalWithHighlights($"Success. Created {newContentTypeId} in {ContentfulEnvironmentId}", Globals.StyleHeading);
        }

        var createEntries = ContentfulEntryEnumerator.Entries<Entry<JObject>>(ContentfulManagementClient, oldContentTypeId)
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();

        bulkActions.AddRange(
            [
                new DeleteBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentTypeOld)
                    .WithContentLocales(ContentLocales)
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                    .WithConcurrentTaskLimit(25)
                    .WithPublishChunkSize(100)
                    .WithMillisecondsBetweenCalls(120),
                new UpsertBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentTypeNew!)
                    .WithContentLocales(ContentLocales)
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
                    .WithNewEntries(createEntries)
                    .WithConcurrentTaskLimit(25)
                    .WithPublishChunkSize(100)
                    .WithMillisecondsBetweenCalls(120)
            ]
        );

        if (settings.Publish)
        {
            bulkActions.Add(
                new PublishBulkAction(_contentfulConnection, _httpClient)
                    .WithContentType(contentTypeNew)
                    .WithContentLocales(ContentLocales)
                    .WithDisplayAction(m => _console.WriteNormalWithHighlights(m, Globals.StyleHeading))
            );
        }

        await PerformBulkOperations(bulkActions.ToArray());

        // 1: fix refs

        var allContentTypes = (await ContentfulManagementClient.GetContentTypes()).OrderBy(c => c.SystemProperties.Id);

        var changedTypes = new Dictionary<string, ContentType>();

        foreach (var contentType in allContentTypes)
        {
            foreach (var field in contentType.Fields)
            {
                if (field.Validations is not null)
                {
                    foreach (var validation in field.Validations)
                    {
                        if (validation is LinkContentTypeValidator linkValidation)
                        {
                            if (!linkValidation.ContentTypeIds.Contains(oldContentTypeId))
                            {
                                continue;
                            }

                            linkValidation.ContentTypeIds.Remove(oldContentTypeId);

                            if (!linkValidation.ContentTypeIds.Contains(newContentTypeId))
                            {
                                linkValidation.ContentTypeIds.Add(newContentTypeId);
                            }
                            changedTypes.TryAdd(contentType.SystemProperties.Id, contentType);
                        }
                    }
                }
                if (field.Items is not null && field.Items.Validations is not null)
                {
                    foreach (var validation in field.Items.Validations)
                    {
                        if (validation is LinkContentTypeValidator linkValidation)
                        {
                            if (!linkValidation.ContentTypeIds.Contains(oldContentTypeId))
                            {
                                continue;
                            }

                            linkValidation.ContentTypeIds.Remove(oldContentTypeId);

                            if (!linkValidation.ContentTypeIds.Contains(newContentTypeId))
                            {
                                linkValidation.ContentTypeIds.Add(newContentTypeId);
                            }
                            changedTypes.TryAdd(contentType.SystemProperties.Id, contentType);
                        }
                    }
                }
            }
        }

        foreach (var (contentTypeId, contentType) in changedTypes)
        {
            _console.WriteNormalWithHighlights($"Updating reference for content type {contentTypeId}", Globals.StyleHeading);
            await ContentfulManagementClient.CreateOrUpdateContentType(contentType,
                version: contentType.SystemProperties.Version);
        }

        // 2: remove old content type

        await ContentfulManagementClient.DeactivateContentType(oldContentTypeId);

        await ContentfulManagementClient.DeleteContentType(oldContentTypeId);

        _console.WriteBlankLine();
        _console.WriteAlert("Done!");

        return 0;
    }

    private async Task<int> CheckAndApplyNamingConventions()
    {
        _console.WriteNormal("Reading all content types..");

        var namespaces = ContentTypes
            .Select(t => t.SystemProperties.Id)
            .Select(i => i.SplitCamelCase()[0])
            .ToHashSet();

        foreach (var contentType in ContentTypes.OrderBy(c => c.SystemProperties.Id))
        {
            var isChanged = false;

            _console.WriteNormalWithHighlights($"Content type '{contentType.SystemProperties.Id}':", Globals.StyleHeading);

            if (contentType.Name != contentType.SystemProperties.Id)
            {
                _console.WriteNormalWithHighlights($"...Renaming content type from '{contentType.Name}' to '{contentType.SystemProperties.Id}'", Globals.StyleHeading);
                contentType.Name = contentType.SystemProperties.Id;
                isChanged = true;
            }

            foreach (var field in contentType.Fields)
            {
                if (field.Id != field.Id.ToCamelCase())
                {
                    _console.WriteAlert($"......rename field id from '{field.Id}' to '{field.Id.ToCamelCase()}'");
                }

                if (field.Name != field.Id)
                {
                    _console.WriteNormalWithHighlights($"......renaming field from '{field.Name}' to '{field.Id}'", Globals.StyleHeading);
                    field.Name = field.Id;
                    isChanged = true;
                }

                if (field.Type == "Link" && field.LinkType == "Entry" && field.Validations is not null && field.Validations.Count > 0)
                {
                    var validation = field.Validations.Where(r => r is LinkContentTypeValidator).FirstOrDefault();

                    if (validation is null)
                    {
                        _console.WriteAlert($"......validation for field '{field.Id}' of type 'Link' should not be null!");
                    }
                    else if (validation is LinkContentTypeValidator linkValidator)
                    {
                        if (linkValidator.ContentTypeIds.Count == 0)
                        {
                            _console.WriteAlert($"......validation for field '{field.Id}' of type 'Link' should have contentType entries!");
                        }
                        else if (linkValidator.ContentTypeIds.Count == 1)
                        {
                            if (linkValidator.ContentTypeIds[0] == contentType.SystemProperties.Id)
                            {
                                var standardName = $"{linkValidator.ContentTypeIds[0]}Parent";
                                var standardNameEnding = $"{linkValidator.ContentTypeIds[0].CamelToPascalCase()}Parent";
                                if (field.Id != standardName && !field.Id.EndsWith(standardNameEnding))
                                {
                                    _console.WriteAlert($"......please rename field '{field.Id}' to '{standardName}' or to end with '{standardNameEnding}'");
                                }
                            }
                            else
                            {
                                var standardName = $"{linkValidator.ContentTypeIds[0]}Entry";
                                var standardNameEnding = $"{linkValidator.ContentTypeIds[0].CamelToPascalCase()}Entry";
                                if (field.Id != standardName && !field.Id.EndsWith(standardNameEnding))
                                {
                                    _console.WriteAlert($"......please rename field '{field.Id}' to '{standardName}' or to end with '{standardNameEnding}'");
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in linkValidator.ContentTypeIds)
                            {
                                if (!field.Id.EndsWith("Entry"))
                                {
                                    _console.WriteAlert($"......'{field.Id}' contains type '{item}'...give it a sensible name ending with 'Entry'");
                                }
                            }
                        }
                        if (!namespaces.Any(field.Id.StartsWith))
                        {
                            var secondSegment = field.Id.SplitCamelCase();
                            if (secondSegment.Length < 2 || !namespaces.Any(s => secondSegment[1].Equals(s, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                _console.WriteDim($"......optionally rename field '{field.Id}' to start with a valid namespace '{string.Join(',', namespaces)}'");
                            }
                        }
                    }
                }
                else if (field.Type == "Array" && field.Items is not null && field.Items.Type == "Link" && field.Items.LinkType == "Entry")
                {
                    var validation = field.Items.Validations.Where(r => r is LinkContentTypeValidator).FirstOrDefault();

                    if (validation is null)
                    {
                        _console.WriteAlert($"......validation for field '{field.Id}' of type 'Array' + 'Link' should not be null!");
                    }
                    else if (validation is LinkContentTypeValidator linkValidator)
                    {
                        if (linkValidator.ContentTypeIds.Count == 0)
                        {
                            _console.WriteAlert($"......validation for field '{field.Id}' of type 'Array' + 'Link' should have contentType entries!");
                        }
                        else if (linkValidator.ContentTypeIds.Count == 1)
                        {
                            if (linkValidator.ContentTypeIds[0] == contentType.SystemProperties.Id)
                            {
                                var standardName = $"{linkValidator.ContentTypeIds[0]}Parents";
                                var standardNameEnding = $"{linkValidator.ContentTypeIds[0].CamelToPascalCase()}Parent";
                                if (field.Id != standardName && !field.Id.EndsWith(standardNameEnding))
                                {
                                    _console.WriteAlert($"......please rename field '{field.Id}' to '{standardName}' or to end with '{standardNameEnding}");
                                }
                            }
                            else
                            {
                                var standardName = $"{linkValidator.ContentTypeIds[0]}Entries";
                                var standardNameEnding = $"{linkValidator.ContentTypeIds[0].CamelToPascalCase()}Entries";
                                if (field.Id != standardName && !field.Id.EndsWith(standardNameEnding))
                                {
                                    _console.WriteAlert($"......please rename field '{field.Id}' to '{standardName}' or to end with '{standardNameEnding}'");
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in linkValidator.ContentTypeIds)
                            {
                                if (!field.Id.EndsWith("Entries"))
                                {
                                    _console.WriteAlert($"......'{field.Id}' contains type '{item}'...give it a sensible name ending with 'Entries'");
                                }
                            }
                        }
                        if (!namespaces.Any(field.Id.StartsWith))
                        {
                            var secondSegment = field.Id.SplitCamelCase();
                            if (secondSegment.Length < 2 || !namespaces.Any(s => secondSegment[1].Equals(s, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                _console.WriteDim($"......optionally rename field '{field.Id}' to start with a valid namespace '{string.Join(',', namespaces)}'");
                            }
                        }
                    }
                }
            }

            if (isChanged)
            {
                _console.WriteNormalWithHighlights($"...saving changes to '{contentType.SystemProperties.Id}'", Globals.StyleHeading);
                await ContentfulManagementClient.CreateOrUpdateContentType(contentType, version: contentType.SystemProperties.Version);
            }
        }

        _console.WriteAlert("Done.");

        return 0;
    }
}