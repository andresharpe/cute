using Cut.Lib.Enums;

namespace Cut.Lib.InputAdapters;

internal class InputAdapterFactory
{
    public static IInputAdapter Create(InputFileFormat fileType, string contentName, string? fileName = null)
    {
        return fileType switch
        {
            InputFileFormat.Excel => new ExcelInputAdapter(contentName, fileName),

            InputFileFormat.Csv => new CsvInputAdapter(contentName, fileName),

            InputFileFormat.Tsv => new CsvInputAdapter(contentName, fileName, "\t"),

            InputFileFormat.Json => new JsonInputAdapter(contentName, fileName),

            InputFileFormat.Yaml => new YamlInputAdapter(contentName, fileName),

            _ => throw new Exception($"No data adapter exists matching {fileType}."),
        };
    }
}