#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using Contentful.Core.Models;
using Cute.Lib.Enums;
using System.Text.RegularExpressions;

namespace Cute.Lib.Contentful.CommandModels.ContentGenerateCommand;

public class CuteContentGenerateContentType
{
    private static readonly ContentType _contentType;

    public static ContentType Instance()
    {
        return _contentType;
    }

    static CuteContentGenerateContentType()
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
                .IsRequired()
                .DefaultValue("en", 1200)
                .Build(),

            new FieldBuilder("temperature", FieldType.Number)
                .IsRequired()
                .ValidateInRange(0,2)
                .DefaultValue("en", 0.8f)
                .Build(),

            new FieldBuilder("topP", FieldType.Number)
                .IsRequired()
                .ValidateInRange(0,1)
                .DefaultValue("en", 0.95f)
                .Build(),

            new FieldBuilder("frequencyPenalty", FieldType.Number)
                .IsRequired()
                .ValidateInRange(0,2)
                .DefaultValue("en", 0.0f)
                .Build(),

            new FieldBuilder("presencePenalty", FieldType.Number)
                .IsRequired()
                .ValidateInRange(0,2)
                .DefaultValue("en", 0.0f)
                .Build(),

            new FieldBuilder("cuteDataQueryEntry", FieldType.Link)
                .IsRequired()
                .ValidateLinkContentType(["cuteDataQuery"], LinkType.Entry)
                .Build(),

            new FieldBuilder("promptOutputContentField", FieldType.Symbol)
                .IsRequired()
                .Build(),
            ]);

        _contentType = contentTypeBuilder.Build();
    }
}