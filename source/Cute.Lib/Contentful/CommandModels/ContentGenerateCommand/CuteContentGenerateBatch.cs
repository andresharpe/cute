using Contentful.Core;
using Contentful.Core.Models;
using Newtonsoft.Json.Linq;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteContentGenerateBatch
{
    public SystemProperties Sys { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Title { get; set; } = default!;
    public CuteContentGenerate CuteContentGenerateEntry { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? ExpiredAt { get; set; }
    public int? CompletionTokens { get; set; }
    public int? PromptTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string TargetContentType { get; set; } = default!;
    public string TargetField { get; set; } = default!;
    public int TargetEntriesCount { get; set; } = default!;

    public bool IsPending()
    {
        /*
            validating	The input file is being validated before the batch processing can begin.
            in_progress	The input file was successfully validated and the batch is currently running.
            finalizing	The batch has completed and the results are being prepared.

            failed	The input file has failed the validation process.
            completed	The batch has been completed and the results are ready.
            expired	    The batch was not able to be completed within the 24-hour time window.
            cancelling	The batch is being cancelled (This can take up to 10 minutes to go into effect.)
            cancelled	the batch was cancelled.

            completed-and-applied <- cute status
        */

        string[] inProgress = ["validating", "in_progress", "finalizing"];

        return inProgress.Contains(Status);
    }

    public Entry<JObject> ToEntry(string defaultLocale) => new()
    {
        SystemProperties = Sys,
        Metadata = new(),
        Fields = new()
        {
            ["key"] = new JObject
            {
                [defaultLocale] = Key
            },
            ["title"] = new JObject
            {
                [defaultLocale] = Title
            },
            ["cuteContentGenerateEntry"] = new JObject
            {
                [defaultLocale] = new JObject
                {
                    ["sys"] = new JObject
                    {
                        ["type"] = "Link",
                        ["linkType"] = "Entry",
                        ["id"] = CuteContentGenerateEntry.Sys.Id
                    }
                }
            },
            ["status"] = new JObject
            {
                [defaultLocale] = Status
            },
            ["createdAt"] = new JObject
            {
                [defaultLocale] = CreatedAt
            },
            ["completedAt"] = new JObject
            {
                [defaultLocale] = CompletedAt
            },
            ["appliedAt"] = new JObject
            {
                [defaultLocale] = AppliedAt
            },
            ["cancelledAt"] = new JObject
            {
                [defaultLocale] = CancelledAt
            },
            ["failedAt"] = new JObject
            {
                [defaultLocale] = FailedAt
            },
            ["expiredAt"] = new JObject
            {
                [defaultLocale] = ExpiredAt
            },
            ["completionTokens"] = new JObject
            {
                [defaultLocale] = CompletionTokens
            },
            ["promptTokens"] = new JObject
            {
                [defaultLocale] = PromptTokens
            },
            ["totalTokens"] = new JObject
            {
                [defaultLocale] = TotalTokens
            },
            ["targetContentType"] = new JObject
            {
                [defaultLocale] = TargetContentType
            },
            ["targetField"] = new JObject
            {
                [defaultLocale] = TargetField
            },
            ["targetEntriesCount"] = new JObject
            {
                [defaultLocale] = TargetEntriesCount
            }
        }
    };

    public static CuteContentGenerateBatch? GetByKey(ContentfulClient contentfulClient, string key)
    {
        return ContentfulEntryEnumerator
            .DeliveryEntries<CuteContentGenerateBatch>(
                contentfulClient,
                "cuteContentGenerateBatch",
                pageSize: 1,
                queryConfigurator: b => b.FieldEquals("fields.key", key)
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .FirstOrDefault();
    }

    public static IReadOnlyList<CuteContentGenerateBatch> GetAll(ContentfulClient contentfulClient)
    {
        return ContentfulEntryEnumerator
            .DeliveryEntries<CuteContentGenerateBatch>(
                contentfulClient,
                "cuteContentGenerateBatch",
                pageSize: 1000
            )
            .ToBlockingEnumerable()
            .Select(e => e.Entry)
            .ToList();
    }
}