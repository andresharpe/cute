using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Enums;

namespace Cute.Lib.Contentful.CommandModels;

public class SchemaBuilder(FieldType fieldType, LinkType? linkType = null)
{
    private readonly Schema _link = new()
    {
        Type = fieldType.ToString(),
        Validations = [],
        LinkType = linkType?.ToString()
    };

    public SchemaBuilder ValidateUnique()
    {
        if (_link.Validations.OfType<UniqueValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new UniqueValidator());
        return this;
    }

    public SchemaBuilder ValidateInRange(int min, int? max, string? message = null)
    {
        if (_link.Validations.OfType<RangeValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new RangeValidator(min, max, message));
        return this;
    }

    public SchemaBuilder ValidateLinkContentType(string[] contentTypeIds, string? message = null)
    {
        if (_link.Validations.OfType<LinkContentTypeValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new LinkContentTypeValidator(message: message, contentTypeIds: contentTypeIds));
        return this;
    }

    public SchemaBuilder ValidateRegex(string expression, string flags = "", string? message = null)
    {
        if (_link.Validations.OfType<RegexValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new RegexValidator(expression, flags, message));
        return this;
    }

    public SchemaBuilder ValidateSize(int min, int max, string? message = null)
    {
        if (_link.Validations.OfType<SizeValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new SizeValidator(min, max, message));
        return this;
    }

    public SchemaBuilder ValidateInValues(string[] requiredValues, string? message = null)
    {
        if (_link.Validations.OfType<InValuesValidator>().Any())
        {
            return this;
        }
        _link.Validations.Add(new InValuesValidator(message: message, requiredValues: requiredValues));
        return this;
    }

    public Schema Build()
    {
        return _link;
    }
}