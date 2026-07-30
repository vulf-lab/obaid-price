using ClosedXML.Excel;

namespace CostWise.App.Services.Import;

/// <summary>Builds Excel workbooks matching formulation / RM / nutrition import parsers.</summary>
public static class ImportSampleWorkbookFactory
{
    public static void SaveRawMaterialSample(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Prices");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Price";
        ws.Cell(2, 1).Value = "Example RM";
        ws.Cell(2, 2).Value = 45000;
        StyleHeader(ws, 2);
        wb.SaveAs(filePath);
    }

    public static void SaveFormulationSample(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Formulas");
        var headers = new[]
        {
            "Category", "Size", "Specie", "Feed Type", "Code", "Rev", "Sub-Category", "RM/Description", "%"
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        // One code, one ingredient at 100% — replace with real masters/RMs before import.
        ws.Cell(2, 1).Value = "Standard";
        ws.Cell(2, 2).Value = "2mm";
        ws.Cell(2, 3).Value = "Tilapia";
        ws.Cell(2, 4).Value = "Grower";
        ws.Cell(2, 5).Value = "SAMPLE01";
        ws.Cell(2, 6).Value = 1;
        ws.Cell(2, 7).Value = "V7";
        ws.Cell(2, 8).Value = "Example RM";
        ws.Cell(2, 9).Value = 100;

        StyleHeader(ws, headers.Length);
        wb.SaveAs(filePath);
    }

    public static void SaveNutritionProfileSample(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Profile");

        // Exact Excel headers from NutritionProfileImportService.HeaderToSpecName (sheet order).
        var headers = new[]
        {
            "CODE",
            "MOISTURE", "FAT", "ASH", "PROTEIN", "DP TILAPIA", "FIBER", "STARCH",
            "DE TILAPIA", "DP:DE", "CALCIUM", "AV PHOSPHORUS", "Ca:P",
            "AV LYSINE", "AV METHIONINE", "AV THREONINE",
            "VITAMINE A", "VITAMINE C", "VITAMINE D3", "VITAMINE E",
            "MET SID / LYS SID Aqua", "THR SID / LYS SID Aqua"
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        ws.Cell(2, 1).Value = "SAMPLE01";
        ws.Cell(2, 2).Value = 9.0;
        ws.Cell(2, 3).Value = 4.9;
        ws.Cell(2, 4).Value = 12.2;
        ws.Cell(2, 5).Value = 35.2;
        ws.Cell(2, 6).Value = 30.8;
        ws.Cell(2, 7).Value = 9.1;
        ws.Cell(2, 8).Value = 13.1;
        ws.Cell(2, 9).Value = 2877;
        ws.Cell(2, 10).Value = 107;
        ws.Cell(2, 11).Value = 20.9;
        ws.Cell(2, 12).Value = 8.8;
        ws.Cell(2, 13).Value = 1.5;
        ws.Cell(2, 14).Value = 19.5;
        ws.Cell(2, 15).Value = 7.3;
        ws.Cell(2, 16).Value = 11.9;
        ws.Cell(2, 17).Value = 8000;
        ws.Cell(2, 18).Value = 82;
        ws.Cell(2, 19).Value = 1200;
        ws.Cell(2, 20).Value = 80;
        ws.Cell(2, 21).Value = 0.37;
        ws.Cell(2, 22).Value = 0.61;

        StyleHeader(ws, headers.Length);
        wb.SaveAs(filePath);
    }

    private static void StyleHeader(IXLWorksheet ws, int columnCount)
    {
        var range = ws.Range(1, 1, 1, columnCount);
        range.Style.Font.Bold = true;
        ws.Columns(1, columnCount).AdjustToContents();
    }
}
