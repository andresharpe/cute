using ClosedXML.Excel;
using Cute.Lib.Exceptions;
using Cute.Lib.OutputAdapters;

namespace Cute.Lib.InputAdapters.FileAdapters;

internal class ExcelInputAdapter : InputAdapterBase
{
    private readonly XLWorkbook _workbook;

    private readonly IXLWorksheet _sheet;

    private int _xlRow = 2;

    private readonly List<string> _columns = [];

    public ExcelInputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".xlsx")
    {
        var loadOptions = new LoadOptions()
        {
            RecalculateAllFormulas = false,
        };

        _workbook = new XLWorkbook(SourceName, loadOptions);

        if (!_workbook.TryGetWorksheet(contentName, out _sheet))
        {
            throw new CliException($"Workbook '{SourceName}' does not contain a sheet named '{contentName}'.");
        }

        ReadHeaders();
    }

    private void ReadHeaders()
    {
        var row = 1;
        var col = 1;
        while (!_sheet.Cell(row, col).Value.IsBlank)
        {
            var columnName = _sheet.Cell(row, col).GetString();
            if (columnName != OutputAdapterBase.StateColumnName)
            {
                _columns.Add(columnName);
            }
            col++;
        }
    }

    public override void Dispose()
    {
        _workbook.Dispose();
    }

    public override Task<IDictionary<string, object?>?> GetRecordAsync()

    {
        var result = new Dictionary<string, object?>();
        var row = _xlRow++;
        var col = 1;
        var isRowBlank = true;

        foreach (var key in _columns)
        {
            var cell = _sheet.Cell(row, col);

            isRowBlank = isRowBlank && cell.Value.IsBlank;

            if (cell.Value.IsBlank || cell.Value.IsError)
            {
                result.Add(key, null);
            }
            else if (cell.Value.IsText)
            {
                result.Add(key, cell.GetValue<string>());
            }
            else if (cell.Value.IsBoolean)
            {
                result.Add(key, cell.GetValue<bool>());
            }
            else if (cell.Value.IsNumber)
            {
                result.Add(key, cell.GetValue<double>());
            }
            else if (cell.Value.IsNumber)
            {
                result.Add(key, cell.GetValue<double>());
            }
            else if (cell.Value.IsDateTime)
            {
                result.Add(key, cell.GetValue<DateTime>());
            }
            else if (cell.Value.IsTimeSpan)
            {
                result.Add(key, cell.GetValue<TimeSpan>());
            }
            else
            {
                result.Add(key, cell.Value);
            }
            col++;
        }

        return Task.FromResult<IDictionary<string, object?>?>(isRowBlank ? null : result);
    }

    public override Task<int> GetRecordCountAsync()
    {
        return Task.FromResult(_sheet.LastRowUsed()?.RowNumber() - 1 ?? 1);
    }
}