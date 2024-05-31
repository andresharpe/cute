
using Cut.Exceptions;

namespace Cut.DataAdapters;

internal class DataAdapterFactory
{
    public static IDataAdapter Create(OutputFileFormat fileType, string contentName, string? fileName = null)
    {
        return fileType switch
        {
            OutputFileFormat.Excel => new ExcelAdapter(contentName, fileName),
            OutputFileFormat.Csv => new CsvAdapter(contentName, fileName),
            OutputFileFormat.Tsv => new CsvAdapter(contentName, fileName, "\t"),
            OutputFileFormat.Json => new JsonAdapter(contentName, fileName),
            OutputFileFormat.Yaml => new YamlAdapter(contentName, fileName),
            _ => throw new CliException($"No data adapter exists matching {fileType}."),
        };
    }
}
