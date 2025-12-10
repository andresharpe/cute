#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteContentGenerateContentType
{
    public static ContentType GetContentType(string locale)
    {
        var contentTypeBuilder = new ContentTypeBuilder("cuteContentGenerate")
            .WithDescription("Prompts designated for AI to generate and translate content for any content type and field.")
            .WithDisplayField("title")
            .WithFields([

            new FieldBuilder("key", FieldType.Symbol)
                .IsRequired()
                .IsUnique()
                .ValidateSize(2,64)
                .ValidateRegex(new Regex("[A-Z_]+").ToString())
                .Build(),

            new FieldBuilder("title", FieldType.Symbol)
                .IsRequired()
                .IsUnique()
                .Build(),

            new FieldBuilder("systemMessage", FieldType.Text)
                .IsRequired()
                .IsLocalized()
                .Build(),

            new FieldBuilder("prompt", FieldType.Text)
                .IsRequired()
                .IsLocalized()
                .Build(),

            new FieldBuilder("deploymentModel", FieldType.Symbol)
                .IsRequired()
                .ValidateInValues(["dep-gpt-4-32k","dep-gpt-4","dep-gpt-4o"])
                .Build(),

            new FieldBuilder("maxTokenLimit", FieldType.Integer)
                .DefaultValue(locale, 1200)
                .Build(),

            new FieldBuilder("temperature", FieldType.Number)
                .ValidateInRange(0,2)
                .DefaultValue(locale, 0.8f)
                .Build(),

            new FieldBuilder("topP", FieldType.Number)
                .ValidateInRange(0,1)
                .DefaultValue(locale, 0.95f)
                .Build(),

            new FieldBuilder("frequencyPenalty", FieldType.Number)
                .ValidateInRange(0,2)
                .DefaultValue(locale, 0.0f)
                .Build(),

            new FieldBuilder("presencePenalty", FieldType.Number)
                .ValidateInRange(0,2)
                .DefaultValue(locale, 0.0f)
                .Build(),

            new FieldBuilder("cuteDataQueryEntry", FieldType.Link)
                .IsRequired()
                .ValidateLinkContentType(["cuteDataQuery"], LinkType.Entry)
                .Build(),

            new FieldBuilder("promptOutputContentField", FieldType.Symbol)
                .IsRequired()
                .Build(),
            ]);

        return contentTypeBuilder.Build();
    }
}