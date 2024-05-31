

using ClosedXML.Excel;
using System.Data;

namespace Cut.DataAdapters;

internal class ExcelAdapter : DataAdapterBase
{
    private readonly XLWorkbook _workbook;
    private readonly IXLWorksheet _sheet;

    private int _xlRow = 2;
    private int _xlHeadingCol = 1;

    private readonly List<string> _columns = new List<string>();

    public ExcelAdapter(string contentName, string? fileName = null) : base(contentName, fileName ?? contentName + ".xlsx")
    {
        _workbook = new XLWorkbook();
        _sheet = _workbook.Worksheets.Add(contentName);
    }

    public override void Dispose()
    {
        _workbook.Dispose();
    }

    public override void AddHeadings(DataTable table)
    {
        foreach (DataColumn col in table.Columns)
        {
            AddHeading(col.ColumnName);
        }
    }

    public override void AddRow(DataRow row)
    {
        var xlCol = 1;
        var xlRow = _xlRow;
        foreach (var val in row.ItemArray)
        {
            _sheet.Cell(xlRow, xlCol).Value = val?.ToString();
            if (_columns[xlCol - 1].StartsWith("sys."))
            {
                _sheet.Cell(xlRow, xlCol).Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            xlCol++;
        }
        _xlRow++;
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
