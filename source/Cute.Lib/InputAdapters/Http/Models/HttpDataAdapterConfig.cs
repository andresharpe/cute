using Cute.Lib.Exceptions;
using Scriban;

namespace Cute.Lib.InputAdapters.Http.Models;

public class HttpDataAdapterConfig
{
    public string Id { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ContentDisplayField { get; set; } = default!;
    public string ContentKeyField { get; set; } = default!;
    public string EndPoint { get; set; } = default!;
    public string ContinuationTokenHeader { get; set; } = default!;
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
    public Dictionary<string, string> Headers { get; set; } = default!;
    public Dictionary<string, string> FormUrlEncodedContent { get; set; } = default!;
    public ResultsFormat ResultsFormat { get; set; } = ResultsFormat.Json;
    public string ResultsSecret { get; set; } = default!;
    public string ResultsJsonPath { get; set; } = default!;
    public FieldMapping[] Mapping { get; set; } = [];
    public VarMapping[] PreMapping { get; set; } = [];
    public List<ContentEntryDefinition> EnumerateForContentTypes { get; set; } = [];
    public string FilterExpression { get; set; } = default!;
    public Pagination Pagination { get; set; } = default!;

    internal Dictionary<string, Template> CompileMappingTemplates()
    {
        var templates = Mapping.ToDictionary(m => m.FieldName, m => Template.Parse(m.Expression));

        var errors = new List<string>();

        foreach (var (fieldName, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for field '{fieldName}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        return templates;
    }

    internal Dictionary<string, Template> CompilePreMappingTemplates()
    {
        if (PreMapping == null) return [];

        var templates = PreMapping.ToDictionary(m => m.VarName, m => Template.Parse(m.Expression));

        var errors = new List<string>();

        foreach (var (varName, template) in templates)
        {
            if (template.HasErrors)
            {
                errors.Add($"Error(s) in mapping for variable '{varName}'.{template.Messages.Select(m => $"\n...{m.Message}")} ");
            }
        }

        if (errors.Count != 0) throw new CliException(string.Join('\n', errors));

        return templates;
    }
}