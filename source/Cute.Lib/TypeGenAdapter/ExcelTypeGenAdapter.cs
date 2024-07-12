using ClosedXML.Excel;
using Contentful.Core.Models;
using Contentful.Core.Models.Management;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Cute.Lib.TypeGenAdapter;

// typegen -o c:\temp -l Excel

public class ExcelTypeGenAdapter : ITypeGenAdapter
{
    private string _fileName = default!;
    private XLWorkbook _workbook = default!;

    public Task PreGenerateTypeSource(List<ContentType> contentTypes, string path, string? fileName = null, string? namespc = null)
    {
        var spaceId = contentTypes.First().SystemProperties.Space.SystemProperties.Id;

        _fileName ??= Path.Combine(path, spaceId + ".xlsx");

        if (System.IO.File.Exists(_fileName))
        {
            System.IO.File.Delete(_fileName);
        }

        _workbook = new XLWorkbook();

        var sheet = _workbook.AddWorksheet("summary");

        var xlRow = 1;
        var xlCol = 1;

        string[] headings = ["Id", "Name", "Description", "DisplayField"];

        foreach (var heading in headings)
        {
            sheet.Cell(xlRow, xlCol).Value = heading;
            sheet.Cell(xlRow, xlCol).Style.Font.SetBold();
            sheet.Cell(xlRow, xlCol).Style.Fill.BackgroundColor = XLColor.LightCarminePink;
            xlCol++;
        }

        xlRow++;

        foreach (var contentType in contentTypes)
        {
            xlCol = 1;

            sheet.Cell(xlRow, xlCol++).Value = contentType.SystemProperties.Id;
            sheet.Cell(xlRow, xlCol++).Value = contentType.Name;
            sheet.Cell(xlRow, xlCol++).Value = contentType.Description;
            sheet.Cell(xlRow, xlCol++).Value = contentType.DisplayField;

            xlRow++;
        }

        sheet.Columns().AdjustToContents();

        return Task.CompletedTask;
    }

    public Task<string> GenerateTypeSource(ContentType contentType, string path, string? fileName = null, string? namespc = null)
    {
        var sheetName = contentType.SystemProperties.Id;

        var sheet = _workbook.AddWorksheet(sheetName);

        var xlRow = 1;
        var xlCol = 1;

        string[] headings = ["Id", "Name", "Type", "LinkType", "ContentTypeIds", "Required", "Localized", "Disabled", "Omitted"];

        foreach (var heading in headings)
        {
            sheet.Cell(xlRow, xlCol).Value = heading;
            sheet.Cell(xlRow, xlCol).Style.Font.SetBold();
            sheet.Cell(xlRow, xlCol).Style.Fill.BackgroundColor = XLColor.LightCarminePink;
            xlCol++;
        }

        xlRow++;

        foreach (var field in contentType.Fields)
        {
            xlCol = 1; ;
            sheet.Cell(xlRow, xlCol++).Value = field.Id;
            sheet.Cell(xlRow, xlCol++).Value = field.Name;
            sheet.Cell(xlRow, xlCol++).Value = field.Type;
            sheet.Cell(xlRow, xlCol++).Value = field.LinkType;

            if (!string.IsNullOrEmpty(field.LinkType))
            {
                var validator = field.Validations.OfType<LinkContentTypeValidator>().SingleOrDefault();
                if (validator != null)
                {
                    sheet.Cell(xlRow, xlCol).Value = string.Join(',', validator.ContentTypeIds);
                }
            }
            xlCol++;

            if (field.Required) sheet.Cell(xlRow, xlCol++).Value = field.Required; else xlCol++;
            if (field.Localized) sheet.Cell(xlRow, xlCol++).Value = field.Localized; else xlCol++;
            if (field.Disabled) sheet.Cell(xlRow, xlCol++).Value = field.Disabled; else xlCol++;
            if (field.Omitted) sheet.Cell(xlRow, xlCol++).Value = field.Omitted; else xlCol++;

            xlRow++;
        }

        sheet.Columns().AdjustToContents();

        return Task.FromResult($"{_fileName} [{sheetName}]");
    }

    public Task PostGenerateTypeSource()
    {
        _workbook.SaveAs(_fileName);

        return Task.CompletedTask;
    }
}