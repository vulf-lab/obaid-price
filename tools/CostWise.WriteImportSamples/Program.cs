using ClosedXML.Excel;

var root = args.Length > 0 ? args[0] : Path.Combine("samples", "import");
Directory.CreateDirectory(root);

SaveRaw(Path.Combine(root, "raw-material-price-import-sample.xlsx"));
SaveFormula(Path.Combine(root, "formulation-import-sample.xlsx"));
SaveNutrition(Path.Combine(root, "nutrition-profile-import-sample.xlsx"));
Console.WriteLine($"Wrote samples to {Path.GetFullPath(root)}");

static void SaveRaw(string path)
{
    using var wb = new XLWorkbook();
    var ws = wb.AddWorksheet("Prices");
    ws.Cell(1, 1).Value = "Name";
    ws.Cell(1, 2).Value = "Price";
    ws.Cell(2, 1).Value = "Example RM";
    ws.Cell(2, 2).Value = 45000;
    ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
    ws.Columns().AdjustToContents();
    wb.SaveAs(path);
}

static void SaveFormula(string path)
{
    using var wb = new XLWorkbook();
    var ws = wb.AddWorksheet("Formulas");
    string[] headers =
    [
        "Category", "Size", "Specie", "Feed Type", "Code", "Rev", "Sub-Category", "RM/Description", "%"
    ];
    for (var i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];
    ws.Cell(2, 1).Value = "Standard";
    ws.Cell(2, 2).Value = "2mm";
    ws.Cell(2, 3).Value = "Tilapia";
    ws.Cell(2, 4).Value = "Grower";
    ws.Cell(2, 5).Value = "SAMPLE01";
    ws.Cell(2, 6).Value = 1;
    ws.Cell(2, 7).Value = "V7";
    ws.Cell(2, 8).Value = "Example RM";
    ws.Cell(2, 9).Value = 100;
    ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
    ws.Columns().AdjustToContents();
    wb.SaveAs(path);
}

static void SaveNutrition(string path)
{
    using var wb = new XLWorkbook();
    var ws = wb.AddWorksheet("Profile");
    string[] headers =
    [
        "CODE",
        "MOISTURE", "FAT", "ASH", "PROTEIN", "DP TILAPIA", "FIBER", "STARCH",
        "DE TILAPIA", "DP:DE", "CALCIUM", "AV PHOSPHORUS", "Ca:P",
        "AV LYSINE", "AV METHIONINE", "AV THREONINE",
        "VITAMINE A", "VITAMINE C", "VITAMINE D3", "VITAMINE E",
        "MET SID / LYS SID Aqua", "THR SID / LYS SID Aqua"
    ];
    for (var i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];
    double?[] vals =
    [
        null, 9.0, 4.9, 12.2, 35.2, 30.8, 9.1, 13.1, 2877, 107, 20.9, 8.8, 1.5,
        19.5, 7.3, 11.9, 8000, 82, 1200, 80, 0.37, 0.61
    ];
    ws.Cell(2, 1).Value = "SAMPLE01";
    for (var i = 1; i < vals.Length; i++)
        ws.Cell(2, i + 1).Value = vals[i];
    ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
    ws.Columns().AdjustToContents();
    wb.SaveAs(path);
}
