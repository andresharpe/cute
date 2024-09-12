using Contentful.Core.Models;
using Cute.Lib.Exceptions;

namespace Cute.Lib.Contentful.CommandModels;

public class ContentTypeBuilder(string contentTypeId)
{
    private readonly ContentType _contentType = new()
    {
        SystemProperties = new()
        {
            Id = contentTypeId,
            Type = nameof(ContentType)
        },
        Name = contentTypeId,
        Fields = []
    };

    public ContentTypeBuilder WithDescription(string description)
    {
        _contentType.Description = description;
        return this;
    }

    public ContentTypeBuilder WithDisplayField(string displayField)
    {
        _contentType.DisplayField = displayField;
        return this;
    }

    public ContentTypeBuilder WithFields(IEnumerable<Field> fields)
    {
        foreach (var field in fields)
        {
            _contentType.Fields.Add(field);
        }

        return this;
    }

    public ContentType Build()
    {
        _contentType.DisplayField ??= ResolveDisplayfield()
            ?? throw new CliException($"No fields added to content type '{_contentType.Name}'");

        var duplicateFieldIds = _contentType.Fields
            .Select(f => f.Id)
            .GroupBy(s => s)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateFieldIds.Count > 0)
        {
            var duplicates = string.Join(", ", duplicateFieldIds);
            throw new CliException($"You have duplicate field Id's for '{duplicates}'");
        }

        return _contentType;
    }

    private string? ResolveDisplayfield()
    {
        if (_contentType.Fields.Count == 0) return null;

        if (_contentType.Fields.Any(f => f.Id.Equals("title", StringComparison.OrdinalIgnoreCase)))
        {
            return _contentType.Fields.First(f => f.Id.Equals("title", StringComparison.OrdinalIgnoreCase)).Id;
        }
        return _contentType.Fields[0].Id;
    }
}