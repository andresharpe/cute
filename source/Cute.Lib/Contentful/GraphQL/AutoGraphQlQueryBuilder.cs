using Contentful.Core.Models.Management;
using Cute.Lib.Extensions;
using Scriban;
using Scriban.Syntax;
using System.Text;

namespace Cute.Lib.Contentful.GraphQL;

public class AutoGraphQlQueryBuilder(ContentfulConnection contentfulConnection)
{
    private bool _isBuilt = false;
    private string _templateContent = default!;
    private readonly List<string> _errors = [];
    private readonly List<string[]> _variableList = [];
    private readonly ContentfulConnection _contentfulConnection = contentfulConnection;
    private string? _contentTypeId = null;

    public IReadOnlyList<string> Errors => _errors;
    public string? ContentTypeId => _contentTypeId;

    public AutoGraphQlQueryBuilder WithTemplateContent(string templateContent)
    {
        _isBuilt = false;
        _variableList.Clear();
        _templateContent = templateContent;
        return this;
    }

    public AutoGraphQlQueryBuilder WithExtraVariables(List<string[]> variables)
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException("Query has already been built. Add variables beforehand.");
        }
        _variableList.AddRange(variables);
        return this;
    }

    public bool TryBuildQuery(out string? query)
    {
        query = null;
        _errors.Clear();

        if (_templateContent is null)
        {
            _errors.Add("Template content is required.");
            return false;
        }

        _isBuilt = true;

        var template = Template.Parse(_templateContent);

        ExtractScribanVariables(template, _variableList);

        ValidateAgainstContentful(_variableList, _errors).Wait();

        if (_errors.Count > 0) return false;

        query = BuildGraphQLQuery(_variableList);

        _contentTypeId = _variableList[0][0];

        return true;
    }

    private async Task ValidateAgainstContentful(List<string[]> variables, List<string> errors)
    {
        var contentTypes = variables.GroupBy(p => p[0]);

        if (!contentTypes.Any())
        {
            errors.Add("No variables found in the template.");
            return;
        }

        if (contentTypes.Count() > 1)
        {
            errors.Add("Variables must be from the same content type.");
        }

        var contentTypeId = contentTypes.First().Key;

        var availableContentTypes = (await _contentfulConnection.GetContentTypesAsync())
            .ToDictionary(ct => ct.Id(), ct => ct.Fields.ToDictionary(f => f.Id));

        if (!availableContentTypes.TryGetValue(contentTypeId, out var mainTargetContentType))
        {
            errors.Add($"Content type '{contentTypeId}' not found in Contentful.");
            return;
        }

        foreach (var variable in variables)
        {
            var contentTypeFields = mainTargetContentType;

            for (int i = 1; i < variable.Length; i++)
            {
                var fieldId = variable[i];

                if (!contentTypeFields.TryGetValue(fieldId, out var targetField))
                {
                    errors.Add($"Field '{fieldId}' not found in content type '{contentTypeId}'.");
                }

                if (targetField?.Type == "Link" && targetField?.LinkType == "Entry")
                {
                    if (variable.Length < 3)
                    {
                        errors.Add($"Link field '{fieldId}' must access one of its fields.");
                        continue;
                    }

                    var linkTypeValidator = targetField.Validations
                        .OfType<LinkContentTypeValidator>()
                        .FirstOrDefault();

                    if (linkTypeValidator is null)
                    {
                        errors.Add($"Link field '{fieldId}' must have a link content type validation.");
                        continue;
                    }

                    var linkContentTypeId = linkTypeValidator.ContentTypeIds;

                    if (linkContentTypeId.Count > 1)
                    {
                        errors.Add($"Link field '{fieldId}' must only have one content type validation.");
                        continue;
                    }

                    if (!availableContentTypes.TryGetValue(linkContentTypeId[0], out contentTypeFields))
                    {
                        errors.Add($"Link content type '{linkContentTypeId[0]}' not found in Contentful.");
                        break;
                    }
                }
            }
        }
    }

    private static void ExtractScribanVariables(Template template, List<string[]> variableList)
    {
        TraverseScriptNode(template.Page, variableList);
    }

    private static void TraverseScriptNode(ScriptNode node, List<string[]> variableList)
    {
        if (node is ScriptMemberExpression globalVariable)
        {
            variableList.Add(globalVariable.ToString().Split('.'));
            return;
        }

        if (node.Children is null) return;

        foreach (var child in node.Children)
        {
            if (child is null) continue;

            TraverseScriptNode(child, variableList);
        }
    }

    private static string BuildGraphQLQuery(List<string[]> fields)
    {
        var sb = new StringBuilder();
        var contentType = fields[0][0]; // The first element of every array refers to the content type

        sb.AppendLine($"query GetContent($locale: String, $preview: Boolean, $skip: Int, $limit: Int) {{");
        sb.AppendLine($"  {contentType}Collection(locale: $locale, preview: $preview, skip: $skip, limit: $limit) {{");
        sb.AppendLine("    items {");

        // Always include sys { id } for the main content type
        sb.AppendLine("      sys { id }");

        // Build the query fields recursively with proper indentation
        var nestedFields = BuildFields(fields, 1, 6); // Pass initial indentation level (6 spaces for nested fields)
        sb.Append(nestedFields);

        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // Recursive method to build nested fields with proper indentation
    private static string BuildFields(List<string[]> fields, int depth, int indentLevel)
    {
        var groupedFields = fields.GroupBy(f => f[depth]).ToList();
        var sb = new StringBuilder();

        foreach (var group in groupedFields)
        {
            var key = group.Key;
            var indent = new string(' ', indentLevel); // Add indentation based on the current level

            // Check if this field group contains nested fields
            var subFields = group.Where(g => g.Length > depth + 1).ToList();

            if (subFields.Any())
            {
                // If there are subfields, open a block for the current field and recursively append subfields
                sb.AppendLine($"{indent}{key} {{");

                // Always include sys { id } for nested subtypes
                sb.AppendLine($"{indent}  sys {{ id }}");

                sb.Append(BuildFields(subFields, depth + 1, indentLevel + 2));
                sb.AppendLine($"{indent}}}");
            }
            else
            {
                // If no subfields, just append the field
                sb.AppendLine($"{indent}{key}");
            }
        }

        return sb.ToString();
    }
}