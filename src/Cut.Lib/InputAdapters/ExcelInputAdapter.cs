using ClosedXML.Excel;

namespace Cut.Lib.InputAdapters;

internal class ExcelInputAdapter : InputAdapterBase
{
    private readonly XLWorkbook _workbook;

    private readonly IXLWorksheet _sheet;

    private int _xlRow = 2;

    private readonly List<string> _columns = [];

    public ExcelInputAdapter(string contentName, string? fileName) : base(fileName ?? contentName + ".xlsx")
    {
        _workbook = new XLWorkbook(FileName);
        _sheet = _workbook.Worksheet(contentName);

        ReadHeaders();
    }

    private void ReadHeaders()
    {
        var row = 1;
        var col = 1;
        while (!_sheet.Cell(row, col).Value.IsBlank)
        {
            _columns.Add(_sheet.Cell(row, col).GetString());
            col++;
        }
    }

    public override void Dispose()
    {
        _workbook.Dispose();
    }

    public override IDictionary<string, object?>? GetRecord()

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

        return isRowBlank ? null : result;
    }

    public override int GetRecordCount()
    {
        return _sheet.LastRowUsed().RowNumber() - 1;
    }

    public override IEnumerable<IDictionary<string, object?>> GetRecords(Action<IDictionary<string, object?>, int>? action = null)
    {
        var result = new List<IDictionary<string, object?>>();
        var row = 1;

        while (true)
        {
            var obj = GetRecord();

            if (obj == null)
            {
                break;
            }

            if (action is not null)
            {
                action(obj, row++);
            }

            result.Add(obj);
        }
        return result;
    }
}