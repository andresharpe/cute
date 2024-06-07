using Cut.Lib.Exceptions;

namespace Cut.Lib.OutputAdapters;

public class OutputAdapterFactory
{
    public static IOutputAdapter Create(OutputFileFormat fileType, string contentName, string? fileName = null)
    {
        return fileType switch
        {
            OutputFileFormat.Excel => new ExcelOutputAdapter(contentName, fileName),
            OutputFileFormat.Csv => new CsvOutputAdapter(contentName, fileName),
            OutputFileFormat.Tsv => new CsvOutputAdapter(contentName, fileName, "\t"),
            OutputFileFormat.Json => new JsonOutputAdapter(contentName, fileName),
            OutputFileFormat.Yaml => new YamlOutputAdapter(contentName, fileName),
            _ => throw new CliException($"No data adapter exists matching {fileType}."),
        };
    }
}