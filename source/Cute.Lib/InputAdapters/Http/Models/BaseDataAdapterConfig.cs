﻿using Cute.Lib.Exceptions;
using Scriban;

namespace Cute.Lib.InputAdapters.Http.Models;

public class BaseDataAdapterConfig
{
    public string Id { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ContentDisplayField { get; set; } = default!;
    public string ContentKeyField { get; set; } = default!;
    public ResultsFormat ResultsFormat { get; set; } = ResultsFormat.Json;
    public string ResultsSecret { get; set; } = default!;
    public string ResultsJsonPath { get; set; } = default!;
    public FieldMapping[] Mapping { get; set; } = [];
    public VarMapping[] PreMapping { get; set; } = [];

    internal Dictionary<Template, Template> CompileMappingTemplates()
    {
        var templates = Mapping.ToDictionary(m => Template.Parse(m.FieldName), m => Template.Parse(m.Expression));

        var errors = new List<string>();

        foreach (var (fieldNameTemplate, valueTemplate) in templates)
        {
            if (fieldNameTemplate.HasErrors)
            {
                errors.Add($"Error(s) in mapping for field name '{fieldNameTemplate}'.{fieldNameTemplate.Messages.Select(m => $"\n...{m.Message}")} ");
            }
            if (valueTemplate.HasErrors)
            {
                errors.Add($"Error(s) in mapping for field expression '{fieldNameTemplate}'.{valueTemplate.Messages.Select(m => $"\n...{m.Message}")} ");
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