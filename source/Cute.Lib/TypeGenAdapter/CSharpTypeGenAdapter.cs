using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using Cute.Lib.Extensions;
using System.Text;

namespace Cute.Lib.TypeGenAdapter;

public class CSharpTypeGenAdapter : ITypeGenAdapter
{
    public Task PreGenerateTypeSource(List<ContentType> contentTypes, string path, string? fileName = null, string? namespc = null)
    {
        return Task.CompletedTask;
    }

    public async Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null)
    {
        fileName ??= Path.Combine(path, contentType.SystemProperties.Id.CamelToPascalCase() + ".cs");

        var ts = new StringBuilder();
        ts.AppendLine("using Contentful.Core.Models;");
        ts.AppendLine();
        if (namespc is not null)
        {
            ts.AppendLine($"namespace {namespc};");
            ts.AppendLine();
        }
        ts.AppendLine($"public class {contentType.SystemProperties.Id.CamelToPascalCase()}");
        ts.AppendLine($"{{");
        foreach (var field in contentType.Fields)
        {
            if (field.Type == "Link")
            {
                ts.AppendLine($"   public object {field.Id.CamelToPascalCase()} {{get; set;}} = default!;");
            }
            else if (field.Type == "Array"
                && field.Items.Validations.Count > 0
                && field.Items.Validations[0] is LinkContentTypeValidator validator)
            {
                ts.AppendLine($"   public List<{validator.ContentTypeIds[0].CamelToPascalCase()}> {field.Id.CamelToPascalCase()} {{ get; set; }} = default!;");
            }
            else
            {
                ts.AppendLine($"   public {ToDotnetType(field.Type)} {field.Id.CamelToPascalCase()} {{get; set;}} = default!;");
            }
        }

        ts.AppendLine($"}}");

        await System.IO.File.WriteAllTextAsync(fileName, ts.ToString());

        return fileName;
    }

    public Task PostGenerateTypeSource()
    {
        return Task.CompletedTask;
    }

    private static string ToDotnetType(string contentfulType)
    {
        return contentfulType switch
        {
            "Symbol" => "string",
            "Text" => "string",
            "RichText" => "Document",
            "Integer" => "int",
            "Number" => "double",
            "Date" => "DateTime",
            "Location" => "Location",
            "Boolean" => "bool",
            "Link" => "object",
            "Array" => "List",
            "Object" => "object",
            _ => throw new NotImplementedException(),
        };
    }
}