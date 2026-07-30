using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CostWise.App.ViewModels;

public sealed class ImportPreviewSection
{
    public ImportPreviewSection(string title, string brushKey, IEnumerable<string> lines)
    {
        Title = title;
        BrushKey = brushKey;
        Lines = lines.ToList();
    }

    public string Title { get; }
    public string BrushKey { get; }
    public IReadOnlyList<string> Lines { get; }
    public bool HasLines => Lines.Count > 0;
}

public partial class ImportPreviewViewModel : ObservableObject
{
    public ImportPreviewViewModel(
        string title,
        string summary,
        IEnumerable<ImportPreviewSection> sections,
        bool canConfirm,
        string confirmLabel = "Confirm import")
    {
        Title = title;
        Summary = summary;
        foreach (var s in sections.Where(x => x.HasLines))
            Sections.Add(s);
        CanConfirm = canConfirm;
        ConfirmLabel = confirmLabel;
    }

    public string Title { get; }
    public string Summary { get; }
    public string ConfirmLabel { get; }
    public ObservableCollection<ImportPreviewSection> Sections { get; } = new();
    public bool CanConfirm { get; }
    public bool Confirmed { get; private set; }

    [RelayCommand]
    private void Confirm(Window? window)
    {
        if (!CanConfirm) return;
        Confirmed = true;
        window?.Close();
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        Confirmed = false;
        window?.Close();
    }
}
