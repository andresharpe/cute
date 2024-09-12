using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Enums;
using Cute.Lib.Exceptions;

namespace Cute.Lib.Contentful.CommandModels;

public class FieldBuilder(string fieldId, FieldType fieldType)
{
    private readonly Field _field = new()
    {
        Id = fieldId,
        Name = fieldId,
        Type = fieldType.ToString(),
        Validations = []
    };

    public FieldBuilder IsLocalized()
    {
        _field.Localized = true;
        return this;
    }

    public FieldBuilder IsRequired()
    {
        _field.Required = true;
        return this;
    }

    public FieldBuilder IsUnique()
    {
        return ValidateUnique();
    }

    public FieldBuilder IsDisabled()
    {
        _field.Disabled = true;
        return this;
    }

    public FieldBuilder IsOmittedFromApi()
    {
        _field.Omitted = true;
        return this;
    }

    public FieldBuilder ValidateUnique()
    {
        if (_field.Validations.OfType<UniqueValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new UniqueValidator());
        return this;
    }

    public FieldBuilder ValidateInRange(int min, int? max, string? message = null)
    {
        if (_field.Validations.OfType<RangeValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new RangeValidator(min, max, message));
        return this;
    }

    public FieldBuilder ValidateLinkContentType(string[] contentTypeIds, LinkType linkType, string? message = null)
    {
        if (_field.Validations.OfType<LinkContentTypeValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new LinkContentTypeValidator(message: message, contentTypeIds: contentTypeIds));
        _field.LinkType = linkType.ToString();
        return this;
    }

    public FieldBuilder ValidateRegex(string expression, string flags = "", string? message = null)
    {
        if (_field.Validations.OfType<RegexValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new RegexValidator(expression, flags, message));
        return this;
    }

    public FieldBuilder ValidateSize(int min, int max, string? message = null)
    {
        if (_field.Validations.OfType<SizeValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new SizeValidator(min, max, message));
        return this;
    }

    public FieldBuilder ValidateInValues(string[] requiredValues, string? message = null)
    {
        if (_field.Validations.OfType<InValuesValidator>().Any())
        {
            return this;
        }
        _field.Validations.Add(new InValuesValidator(message: message, requiredValues: requiredValues));
        return this;
    }

    public FieldBuilder DefaultValue(string locale, object value)
    {
        _field.DefaultValue ??= [];

        _field.DefaultValue[locale] = value;

        return this;
    }

    public FieldBuilder Items(Schema linkSchema)
    {
        _field.Items = linkSchema;
        return this;
    }

    public Field Build()
    {
        if (_field.Type == "Array" && _field.Items == null)
        {
            throw new CliException($"Field '{_field.Id}' is an 'Array' type and must have '.Items(...)' called.");
        }

        if (_field.Type == "Array" && _field.Validations.Count > 0)
        {
            throw new CliException($"Field '{_field.Id}' is an 'Array' and must have all Validations defined on '.Items(...)' call.");
        }

        return _field;
    }
}