using System.Windows;
using CostWise.App.ViewModels;
using CostWise.App.Views;
using CostWise.Core.Services.Import;

namespace CostWise.App.Services.Import;

public static class ImportPreviewDialog
{
    public static bool ShowRmPreview(Window? owner, RmImportPreview preview)
    {
        var sections = new List<ImportPreviewSection>
        {
            new("Missing from file (will leave unchanged)", "Warning",
                preview.Issues.Where(i => i.Kind == RmImportIssueKind.MissingFromFile).Select(i => i.Message)),
            new("Missing / invalid prices (will skip)", "Warning",
                preview.Issues.Where(i => i.Kind == RmImportIssueKind.MissingPrice).Select(i => i.Message)),
            new("New raw ingredients (will create)", "Warning",
                preview.Issues.Where(i => i.Kind == RmImportIssueKind.NewIngredient).Select(i => i.Message)),
            new("Zero / unavailable → new price", "Warning",
                preview.Issues.Where(i => i.Kind == RmImportIssueKind.ReactivatingPrice).Select(i => i.Message)),
            new("Price updates", "Info",
                preview.Issues.Where(i => i.Kind == RmImportIssueKind.WillUpdate).Select(i => i.Message))
        };

        var vm = new ImportPreviewViewModel(
            "Import raw material prices",
            preview.SummaryText,
            sections,
            canConfirm: preview.ActionCount > 0,
            confirmLabel: preview.HasWarnings ? "Confirm despite warnings" : "Confirm import");

        return Show(owner, vm);
    }

    public static bool ShowFormulaPreview(Window? owner, FormulaImportPreview preview)
    {
        var sections = new List<ImportPreviewSection>
        {
            new("Blocked codes (will not update)", "Danger",
                preview.Blocked.SelectMany(r => r.BlockerReasons.Select(b => $"{r.Code}: {b}"))),
            new("Same code, different formula (confirm overwrite)", "Warning",
                preview.Different.Select(r =>
                    $"{r.Code}: {r.Summary}\n  " + string.Join("\n  ", r.DiffLines))),
            new("New formulas", "Info",
                preview.Results.Where(r => r.Disposition == FormulaImportDisposition.New)
                    .Select(r => r.Summary)),
            new("Identical (no write)", "Info",
                preview.Results.Where(r => r.Disposition == FormulaImportDisposition.Same)
                    .Select(r => r.Summary))
        };

        var canConfirm = preview.WillWrite.Count > 0;
        var vm = new ImportPreviewViewModel(
            "Import formulations",
            preview.SummaryText,
            sections,
            canConfirm,
            confirmLabel: preview.HasDifferentCodeWarnings || preview.HasBlockers
                ? "Confirm import (skip blocked)"
                : "Confirm import");

        return Show(owner, vm);
    }

    private static bool Show(Window? owner, ImportPreviewViewModel vm)
    {
        var window = new ImportPreviewWindow
        {
            DataContext = vm,
            Owner = owner
        };
        window.ShowDialog();
        return vm.Confirmed;
    }
}
