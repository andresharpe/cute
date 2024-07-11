using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Extensions;
using System.Text;

namespace Cute.Lib.TypeGenAdapter;

public class TypeScriptTypeGenAdapter : ITypeGenAdapter
{
    public async Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null)
    {
        fileName ??= Path.Combine(path, contentType.SystemProperties.Id.CamelToPascalCase() + ".ts");

        var ts = new StringBuilder();
        ts.AppendLine("import type { EntryFieldTypes } from \"contentful\";");
        ts.AppendLine();
        foreach (var field in contentType.Fields)
        {
            if (field.Type == "Array" && field.Items.Validations[0] is LinkContentTypeValidator validator)
            {
                var importType = validator.ContentTypeIds[0];
                ts.AppendLine($"import type {{ {importType.CamelToPascalCase()} }} from \"./{importType}\";");
            }
        }
        ts.AppendLine();
        ts.AppendLine($"export interface {contentType.SystemProperties.Id.CamelToPascalCase()} {{");
        ts.AppendLine($"   contentTypeId: \"{contentType.SystemProperties.Id}\",");
        ts.AppendLine($"   fields: {{");
        foreach (var field in contentType.Fields)
        {
            if (field.Type == "Link")
            {
                ts.AppendLine($"      {field.Id}: EntryFieldTypes.{field.LinkType}{field.Type},");
            }
            else if (field.Type == "Array" && field.Items.Validations[0] is LinkContentTypeValidator validator)
            {
                ts.AppendLine($"      {field.Id}: EntryFieldTypes.{field.Items.LinkType}{field.Items.Type}<{validator.ContentTypeIds[0].CamelToPascalCase()}>,");
            }
            else
            {
                ts.AppendLine($"      {field.Id}: EntryFieldTypes.{field.Type},");
            }
        }

        ts.AppendLine($"   }}");
        ts.AppendLine($"}}");

        await System.IO.File.WriteAllTextAsync(fileName, ts.ToString());

        return fileName;
    }
}