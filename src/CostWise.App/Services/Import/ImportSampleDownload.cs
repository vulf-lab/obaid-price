using System.Windows;
using Microsoft.Win32;

namespace CostWise.App.Services.Import;

public static class ImportSampleDownload
{
    public static void PromptSave(string defaultFileName, Action<string> save)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            FileName = defaultFileName,
            Title = "Save import sample"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            save(dialog.FileName);
            MessageBox.Show(
                $"Sample saved:\n{dialog.FileName}\n\nFill in your data, then use Import.",
                "Import sample",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not save sample", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
