using ClosedXML.Excel;

namespace Cute.Lib.OutputAdapters;

internal class ExcelOutputAdapter : OutputAdapterBase
{
    private readonly XLWorkbook _workbook;
    private readonly IXLWorksheet _sheet;

    private int _xlRow = 2;
    private int _xlHeadingCol = 1;

    private readonly List<string> _columns = [];

    public ExcelOutputAdapter(string contentName, string? fileName = null) : base(fileName ?? contentName + ".xlsx")
    {
        _workbook = new XLWorkbook();
        _sheet = _workbook.Worksheets.Add(contentName);
    }

    public override void Dispose()
    {
        _workbook.Dispose();
    }

    public override void AddHeadings(IEnumerable<string> headings)
    {
        foreach (var col in headings)
        {
            AddHeading(col);
        }
    }

    public override void AddRow(IDictionary<string, object?> row)
    {
        var xlCol = 1;
        var xlRow = _xlRow;
        foreach (var (_, val) in row)
        {
            _sheet.Cell(xlRow, xlCol).Value = ToExcelCellValue(val);
            if (_columns[xlCol - 1].StartsWith("sys."))
            {
                _sheet.Cell(xlRow, xlCol).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            xlCol++;
        }
        _xlRow++;
    }

    private static XLCellValue ToExcelCellValue(object? val)
    {
        if (val is string @string)
        {
            return @string;
        }
        else if (val is int integer)
        {
            return integer;
        }
        else if (val is double @double)
        {
            return @double;
        }
        else if (val is bool @bool)
        {
            return @bool;
        }
        else if (val is DateTime datetime)
        {
            return datetime;
        }
        else if (val is TimeSpan timespan)
        {
            return timespan;
        }
        return val?.ToString();
    }

    private void AddHeading(string columnName)
    {
        _columns.Add(columnName);
        _sheet.Cell(1, _xlHeadingCol).Value = columnName;
        _sheet.Cell(1, _xlHeadingCol).Style.Font.SetBold();
        _sheet.Cell(1, _xlHeadingCol).Style.Fill.BackgroundColor = XLColor.LightCarminePink;
        _xlHeadingCol++;
    }

    public override void Save()
    {
        _sheet.Columns().AdjustToContents();
        _workbook.SaveAs(FileName);
    }
}