using Cut.Exceptions;

namespace Cut.InputAdapters;

internal class InputAdapterFactory
{
    public static IInputAdapter Create(InputFileFormat fileType, string contentName, string? fileName = null)
    {
        return fileType switch
        {
            InputFileFormat.Excel => new ExcelInputAdapter(contentName, fileName),
            InputFileFormat.Csv => throw new NotImplementedException(),
            InputFileFormat.Tsv => throw new NotImplementedException(),
            InputFileFormat.Json => throw new NotImplementedException(),
            InputFileFormat.Yaml => throw new NotImplementedException(),
            _ => throw new CliException($"No data adapter exists matching {fileType}."),
        };
    }
}