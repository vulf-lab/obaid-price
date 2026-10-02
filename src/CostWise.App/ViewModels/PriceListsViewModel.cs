using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace CostWise.App.ViewModels;

public partial class PriceListColumnToggle : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isVisible = true;
}

public partial class RmPickItem : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

public sealed class VictoryPreviousDateOption
{
    public static VictoryPreviousDateOption Auto { get; } = new(null);

    public VictoryPreviousDateOption(DateTime? dateUtc) => DateUtc = dateUtc?.Date;

    public DateTime? DateUtc { get; }

    public string Label => DateUtc is null
        ? "Auto (last change)"
        : DateUtc.Value.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public override string ToString() => Label;
}

/// <summary>None = no Last/Change columns; Auto = chronologically previous snapshot; else pick by Id.</summary>
public sealed class VictorySellCompareOption
{
    public static VictorySellCompareOption None { get; } = new(null, isAuto: false, "None");
    public static VictorySellCompareOption Auto { get; } = new(null, isAuto: true, "Auto (previous snapshot)");

    public VictorySellCompareOption(int? snapshotId, bool isAuto, string label)
    {
        SnapshotId = snapshotId;
        IsAuto = isAuto;
        Label = label;
    }

    public int? SnapshotId { get; }
    public bool IsAuto { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class CommercialPreviewRow
{
    public string CategoryName { get; init; } = string.Empty;
    public string FeedTypeName { get; init; } = string.Empty;
    public string SizeName { get; init; } = string.Empty;
    public string CpDisplay { get; init; } = "—";
    public string FatDisplay { get; init; } = "—";
    public string SaleDisplay { get; init; } = "—";
}

public sealed class SavedBriefItem
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string BookAName { get; set; } = string.Empty;
    public string BookBName { get; set; } = string.Empty;
    public bool HasBrief { get; set; }

    public string WhenLabel =>
        CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    public string BookSummary => $"{BookAName} · {BookBName}";
    public string CompareLabel => $"{WhenLabel} — {Label}";
}

public sealed class SavedBriefLineRow
{
    public string BookSide { get; init; } = string.Empty;
    public string FormulationCode { get; init; } = string.Empty;
    public string SellMtDisplay { get; init; } = "—";
    public string SellBagDisplay { get; init; } = "—";
    public string CurrencyCode { get; init; } = string.Empty;
}

public partial class PriceListsViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private bool _suppressVictoryPersist;
    private CancellationTokenSource? _victoryPreviewCts;

    public ObservableCollection<PriceBook> VictoryBooks { get; } = new();
    public ObservableCollection<RmPickItem> RawMaterials { get; } = new();
    public ObservableCollection<PriceListColumnToggle> ColumnToggles { get; } = new();
    public ObservableCollection<PriceListColumnToggle> CommercialColumnToggles { get; } = new();
    public ObservableCollection<CommercialPriceList> CommercialLists { get; } = new();
    public ObservableCollection<PriceBook> AvailableBooksForAssign { get; } = new();
    public ObservableCollection<PriceBook> AssignedBooks { get; } = new();
    public ObservableCollection<Currency> Currencies { get; } = new();
    public ObservableCollection<CommercialPreviewRow> CommercialPreviewRows { get; } = new();
    public ObservableCollection<ImageSource> VictoryPreviewPages { get; } = new();
    public ObservableCollection<SavedBriefItem> SavedBriefs { get; } = new();
    public ObservableCollection<SavedBriefItem> FilteredSavedBriefs { get; } = new();
    public ObservableCollection<SavedBriefLineRow> SavedBriefLines { get; } = new();
    public ObservableCollection<ImageSource> SavedBriefPreviewPages { get; } = new();
    /// <summary>Full list of previous-date options (source for filtering).</summary>
    public ObservableCollection<VictoryPreviousDateOption> VictoryPreviousDateOptions { get; } = new();
    public ObservableCollection<VictoryPreviousDateOption> FilteredVictoryPreviousDateOptions { get; } = new();
    [ObservableProperty] private VictoryPreviousDateOption? _selectedVictoryPreviousDate;
    [ObservableProperty] private string _victoryPreviousDateFilter = string.Empty;
    public ObservableCollection<VictorySellCompareOption> VictorySellCompareOptions { get; } = new();
    public ObservableCollection<VictorySellCompareOption> FilteredVictorySellCompareOptions { get; } = new();
    [ObservableProperty] private VictorySellCompareOption? _selectedVictorySellCompare;
    [ObservableProperty] private string _victorySellCompareFilter = string.Empty;
    public PriceUnit[] SellUnitOptions { get; } = [PriceUnit.PerMt, PriceUnit.PerBag25Kg];

    [ObservableProperty] private PriceBook? _victoryBookA;
    [ObservableProperty] private PriceBook? _victoryBookB;
    [ObservableProperty] private string _commentary = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _victoryPreviewStatus = string.Empty;
    [ObservableProperty] private CommercialPriceList? _selectedCommercialList;
    [ObservableProperty] private PriceBook? _bookToAssign;
    [ObservableProperty] private Currency? _editCurrency;
    [ObservableProperty] private PriceUnit _editSellUnit = PriceUnit.PerMt;
    [ObservableProperty] private DateTime _editEffectiveDate = DateTime.Today;
    [ObservableProperty] private string _newCommercialListName = string.Empty;
    [ObservableProperty] private bool _suppressCommercialSync;
    [ObservableProperty] private string _saleColumnHeader = "Sale (Per MT)";
    [ObservableProperty] private bool _showCommercialCategory = true;
    [ObservableProperty] private bool _showCommercialFeedType = true;
    [ObservableProperty] private bool _showCommercialSize = true;
    [ObservableProperty] private bool _showCommercialCp = true;
    [ObservableProperty] private bool _showCommercialFat = true;
    [ObservableProperty] private bool _showCommercialSale = true;
    [ObservableProperty] private int? _renamingListId;
    [ObservableProperty] private string _renameDraftName = string.Empty;
    [ObservableProperty] private decimal _roundSaleTo;
    [ObservableProperty] private decimal _roundCpTo;
    [ObservableProperty] private decimal _roundFatTo;
    [ObservableProperty] private decimal _roundBookATo;
    [ObservableProperty] private decimal _roundBookBTo;
    [ObservableProperty] private decimal _roundDeltaPercentTo;
    [ObservableProperty] private double _victoryPreviewZoom = 1.0;
    [ObservableProperty] private int _selectedPriceListTab;
    [ObservableProperty] private SavedBriefItem? _selectedSavedBrief;
    [ObservableProperty] private string _savedBriefFilter = string.Empty;
    [ObservableProperty] private string _savedBriefPreviewStatus = string.Empty;
    [ObservableProperty] private bool _savedBriefShowsPreview;
    [ObservableProperty] private bool _savedBriefShowsLines;
    [ObservableProperty] private double _savedBriefPreviewZoom = 1.0;

    private CancellationTokenSource? _savedBriefCts;
    private bool _suppressCommercialColumnPersist;

    public PriceListsViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        foreach (var name in PriceListBriefBuilder.AllExportColumns)
        {
            var toggle = new PriceListColumnToggle
            {
                Name = name,
                IsVisible = PriceListBriefBuilder.DefaultVisibleColumns.Contains(name)
            };
            toggle.PropertyChanged += OnVictorySettingPropertyChanged;
            ColumnToggles.Add(toggle);
        }

        _suppressCommercialColumnPersist = true;
        foreach (var name in CommercialPriceListWriter.AllColumns)
        {
            var toggle = new PriceListColumnToggle { Name = name, IsVisible = true };
            toggle.PropertyChanged += OnCommercialColumnPropertyChanged;
            CommercialColumnToggles.Add(toggle);
        }
        RestoreCommercialColumnPrefs();
        SyncCommercialColumnVisibilityFlags();
        _suppressCommercialColumnPersist = false;
        PersistCommercialColumnPrefs();

        _ = LoadAsync();
    }

    public string SellUnitLabel(PriceUnit u) =>
        u == PriceUnit.PerBag25Kg ? "Per bag" : "Per MT";

    [RelayCommand]
    private async Task LoadAsync()
    {
        _suppressVictoryPersist = true;
        await using var db = await _dbFactory.CreateDbContextAsync();
        VictoryBooks.Clear();
        foreach (var b in await db.PriceBooks.AsNoTracking()
                     .Include(x => x.DisplayCurrency)
                     .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync())
            VictoryBooks.Add(b);

        RawMaterials.Clear();
        foreach (var rm in await db.RawIngredients.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
        {
            var item = new RmPickItem { Id = rm.Id, Name = rm.Name };
            item.PropertyChanged += OnVictorySettingPropertyChanged;
            RawMaterials.Add(item);
        }

        RestoreVictoryPrefs();

        VictoryBookA ??= VictoryBooks.FirstOrDefault(b =>
            b.Name.Contains("Victory", StringComparison.OrdinalIgnoreCase));
        VictoryBookB ??= VictoryBooks.FirstOrDefault(b =>
            b.Name.Contains("Kivu", StringComparison.OrdinalIgnoreCase));

        Currencies.Clear();
        foreach (var c in await db.Currencies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync())
            Currencies.Add(c);

        await ReloadVictoryPreviousDateOptionsAsync(db);
        await ReloadVictorySellCompareOptionsAsync(db);
        await ReloadSavedBriefsAsync(db);
        await ReloadCommercialListsAsync(db);
        UpdateSaleColumnHeader();
        _suppressVictoryPersist = false;
        ScheduleVictoryPreviewRefresh();
    }

    private async Task ReloadVictoryPreviousDateOptionsAsync(CostWiseDbContext db)
    {
        var dates = await PriceListBriefBuilder.GetDistinctHistoryDatesUtcAsync(db);
        var selected = _pendingPreviousAsOfUtc ?? SelectedVictoryPreviousDate?.DateUtc;
        VictoryPreviousDateOptions.Clear();
        VictoryPreviousDateOptions.Add(VictoryPreviousDateOption.Auto);
        foreach (var d in dates)
            VictoryPreviousDateOptions.Add(new VictoryPreviousDateOption(d));

        if (selected is DateTime s && VictoryPreviousDateOptions.All(x => x.DateUtc != s))
            VictoryPreviousDateOptions.Insert(1, new VictoryPreviousDateOption(s));

        SelectedVictoryPreviousDate = selected is DateTime keep
            ? VictoryPreviousDateOptions.FirstOrDefault(x => x.DateUtc == keep) ?? VictoryPreviousDateOption.Auto
            : VictoryPreviousDateOption.Auto;
        _pendingPreviousAsOfUtc = null;
        RefreshFilteredVictoryPreviousDates();
    }

    public void ClearVictoryPreviousDateFilter()
    {
        VictoryPreviousDateFilter = string.Empty;
    }

    public void ClearVictorySellCompareFilter()
    {
        VictorySellCompareFilter = string.Empty;
    }

    partial void OnVictoryPreviousDateFilterChanged(string value) => RefreshFilteredVictoryPreviousDates();

    partial void OnVictorySellCompareFilterChanged(string value) => RefreshFilteredVictorySellCompare();

    private void RefreshFilteredVictoryPreviousDates()
    {
        var q = VictoryPreviousDateFilter.Trim();
        FilteredVictoryPreviousDateOptions.Clear();
        foreach (var opt in VictoryPreviousDateOptions)
        {
            if (string.IsNullOrEmpty(q) ||
                opt.Label.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredVictoryPreviousDateOptions.Add(opt);
        }
    }

    private void RefreshFilteredVictorySellCompare()
    {
        var q = VictorySellCompareFilter.Trim();
        FilteredVictorySellCompareOptions.Clear();
        foreach (var opt in VictorySellCompareOptions)
        {
            if (string.IsNullOrEmpty(q) ||
                opt.Label.Contains(q, StringComparison.OrdinalIgnoreCase))
                FilteredVictorySellCompareOptions.Add(opt);
        }
    }

    private void RestoreVictoryPrefs()
    {
        var prefs = VictoryPriceListPrefsStore.Load();
        if (prefs is null) return;

        if (prefs.BookAId is int aId)
            VictoryBookA = VictoryBooks.FirstOrDefault(b => b.Id == aId);
        if (prefs.BookBId is int bId)
            VictoryBookB = VictoryBooks.FirstOrDefault(b => b.Id == bId);

        var selected = new HashSet<int>(prefs.SelectedRmIds ?? []);
        foreach (var rm in RawMaterials)
            rm.IsSelected = selected.Contains(rm.Id);

        if (prefs.VisibleColumns is { Length: > 0 })
        {
            var visible = new HashSet<string>(prefs.VisibleColumns, StringComparer.Ordinal);
            foreach (var col in ColumnToggles)
                col.IsVisible = visible.Contains(col.Name);
        }

        Commentary = prefs.Commentary ?? string.Empty;
        RoundBookATo = prefs.RoundBookATo;
        RoundBookBTo = prefs.RoundBookBTo;
        RoundDeltaPercentTo = prefs.RoundDeltaPercentTo;
        _pendingPreviousAsOfUtc = prefs.PreviousPriceAsOfUtc?.Date;
        _pendingCompareSnapshotId = prefs.CompareSnapshotId;
        _pendingCompareSellAuto = prefs.CompareSellAuto;
    }

    private DateTime? _pendingPreviousAsOfUtc;
    private int? _pendingCompareSnapshotId;
    private bool _pendingCompareSellAuto = true;

    private void PersistVictoryPrefs()
    {
        if (_suppressVictoryPersist) return;
        var compare = SelectedVictorySellCompare;
        VictoryPriceListPrefsStore.Save(new VictoryPriceListPrefs(
            VictoryBookA?.Id,
            VictoryBookB?.Id,
            RawMaterials.Where(x => x.IsSelected).Select(x => x.Id).ToArray(),
            ColumnToggles.Where(c => c.IsVisible).Select(c => c.Name).ToArray(),
            Commentary,
            RoundBookATo,
            RoundBookBTo,
            SelectedVictoryPreviousDate?.DateUtc,
            compare?.IsAuto == true ? null : compare?.SnapshotId,
            compare?.IsAuto == true,
            RoundDeltaPercentTo));
    }

    private void OnVictorySettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RmPickItem.IsSelected) or nameof(PriceListColumnToggle.IsVisible))
        {
            PersistVictoryPrefs();
            ScheduleVictoryPreviewRefresh();
        }
    }

    partial void OnVictoryBookAChanged(PriceBook? value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnVictoryBookBChanged(PriceBook? value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnCommentaryChanged(string value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnRoundBookAToChanged(decimal value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnRoundBookBToChanged(decimal value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnRoundDeltaPercentToChanged(decimal value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnSelectedVictoryPreviousDateChanged(VictoryPreviousDateOption? value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    partial void OnSelectedVictorySellCompareChanged(VictorySellCompareOption? value)
    {
        PersistVictoryPrefs();
        ScheduleVictoryPreviewRefresh();
    }

    public string VictoryPreviewZoomLabel => $"{VictoryPreviewZoom * 100:0}%";

    partial void OnVictoryPreviewZoomChanged(double value) =>
        OnPropertyChanged(nameof(VictoryPreviewZoomLabel));

    [RelayCommand]
    private void ZoomVictoryPreviewIn() =>
        VictoryPreviewZoom = Math.Min(3.0, Math.Round(VictoryPreviewZoom + 0.1, 2));

    [RelayCommand]
    private void ZoomVictoryPreviewOut() =>
        VictoryPreviewZoom = Math.Max(0.5, Math.Round(VictoryPreviewZoom - 0.1, 2));

    public void AdjustVictoryPreviewZoom(int deltaSteps)
    {
        if (deltaSteps == 0) return;
        var next = VictoryPreviewZoom + deltaSteps * 0.1;
        VictoryPreviewZoom = Math.Clamp(Math.Round(next, 2), 0.5, 3.0);
    }

    private void ScheduleVictoryPreviewRefresh()
    {
        _victoryPreviewCts?.Cancel();
        _victoryPreviewCts?.Dispose();
        _victoryPreviewCts = new CancellationTokenSource();
        var token = _victoryPreviewCts.Token;
        _ = DebouncedVictoryPreviewRefreshAsync(token);
    }

    private async Task DebouncedVictoryPreviewRefreshAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            await RefreshVictoryPreviewAsync(token);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer refresh
        }
    }

    private async Task RefreshVictoryPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (VictoryBookA is null || VictoryBookB is null)
        {
            VictoryPreviewPages.Clear();
            VictoryPreviewStatus = "Select both price books.";
            return;
        }

        try
        {
            VictoryPreviewStatus = "Updating preview…";
            var brief = await BuildVictoryBriefAsync();
            if (brief is null || cancellationToken.IsCancellationRequested)
                return;

            var pngPages = await Task.Run(
                () => VictoryGroupPriceListWriter.RenderPreviewImages(brief),
                cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                VictoryPreviewPages.Clear();
                foreach (var png in pngPages)
                    VictoryPreviewPages.Add(ToImageSource(png));
                var unmatched = brief.ShowSellCompare
                    && brief.BookARows.Count + brief.BookBRows.Count > 0
                    && !VictorySellPriceCompare.AnyLastPrice(brief.BookARows, brief.BookBRows);
                VictoryPreviewStatus = unmatched
                    ? "Saved snapshot has no matching formulas for these price books."
                    : VictoryPreviewPages.Count > 0 ? string.Empty : "No preview pages.";
            });
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer refresh
        }
        catch (Exception ex)
        {
            VictoryPreviewStatus = ex.Message;
        }
    }

    private static ImageSource ToImageSource(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void RestoreCommercialColumnPrefs()
    {
        var prefs = CommercialPriceListPrefsStore.Load();
        if (prefs is null) return;
        if (prefs.VisibleColumns is { Length: > 0 })
        {
            var visible = new HashSet<string>(prefs.VisibleColumns, StringComparer.Ordinal);
            var upgradeCategory = prefs.SchemaVersion < 2;
            foreach (var col in CommercialColumnToggles)
            {
                if (col.Name == "Category" && upgradeCategory)
                    col.IsVisible = true;
                else
                    col.IsVisible = visible.Contains(col.Name);
            }
        }

        RoundSaleTo = prefs.RoundSaleTo;
        RoundCpTo = prefs.RoundCpTo;
        RoundFatTo = prefs.RoundFatTo;
    }

    private void PersistCommercialColumnPrefs()
    {
        if (_suppressCommercialColumnPersist) return;
        CommercialPriceListPrefsStore.Save(new CommercialPriceListPrefs(
            CommercialColumnToggles.Where(c => c.IsVisible).Select(c => c.Name).ToArray(),
            RoundSaleTo,
            RoundCpTo,
            RoundFatTo,
            SchemaVersion: 2));
    }

    private void OnCommercialColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PriceListColumnToggle.IsVisible)) return;
        SyncCommercialColumnVisibilityFlags();
        PersistCommercialColumnPrefs();
    }

    partial void OnRoundSaleToChanged(decimal value)
    {
        if (_suppressCommercialColumnPersist) return;
        PersistCommercialColumnPrefs();
        if (!_suppressCommercialSync)
            _ = RefreshCommercialPreviewAsync();
    }

    partial void OnRoundCpToChanged(decimal value)
    {
        if (_suppressCommercialColumnPersist) return;
        PersistCommercialColumnPrefs();
        if (!_suppressCommercialSync)
            _ = RefreshCommercialPreviewAsync();
    }

    partial void OnRoundFatToChanged(decimal value)
    {
        if (_suppressCommercialColumnPersist) return;
        PersistCommercialColumnPrefs();
        if (!_suppressCommercialSync)
            _ = RefreshCommercialPreviewAsync();
    }

    private void SyncCommercialColumnVisibilityFlags()
    {
        ShowCommercialCategory = IsCommercialColVisible("Category");
        ShowCommercialFeedType = IsCommercialColVisible("Feed type");
        ShowCommercialSize = IsCommercialColVisible("Size");
        ShowCommercialCp = IsCommercialColVisible("CP");
        ShowCommercialFat = IsCommercialColVisible("Fat");
        ShowCommercialSale = IsCommercialColVisible("Sale");
    }

    private bool IsCommercialColVisible(string name) =>
        CommercialColumnToggles.FirstOrDefault(c => c.Name == name)?.IsVisible ?? true;

    private IReadOnlyList<string> GetCommercialVisibleColumns()
    {
        var cols = CommercialColumnToggles.Where(c => c.IsVisible).Select(c => c.Name).ToList();
        return cols.Count > 0 ? cols : CommercialPriceListWriter.AllColumns;
    }

    partial void OnEditSellUnitChanged(PriceUnit value)
    {
        UpdateSaleColumnHeader();
        if (!_suppressCommercialSync)
            _ = RefreshCommercialPreviewAsync();
    }

    partial void OnEditCurrencyChanged(Currency? value)
    {
        UpdateSaleColumnHeader();
        if (!_suppressCommercialSync)
            _ = RefreshCommercialPreviewAsync();
    }

    private void UpdateSaleColumnHeader()
    {
        var currency = PriceListNumberFormat.CurrencyLabel(EditCurrency?.Code);
        SaleColumnHeader = $"Sale ({currency} {SellUnitLabel(EditSellUnit)})";
    }

    private async Task ReloadCommercialListsAsync(CostWiseDbContext db)
    {
        var previousId = SelectedCommercialList?.Id;
        CommercialLists.Clear();
        foreach (var list in await db.CommercialPriceLists
                     .Include(x => x.Currency)
                     .Include(x => x.Books).ThenInclude(b => b.PriceBook)
                     .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                     .ToListAsync())
            CommercialLists.Add(list);

        SelectedCommercialList = previousId is int id
            ? CommercialLists.FirstOrDefault(x => x.Id == id) ?? CommercialLists.FirstOrDefault()
            : CommercialLists.FirstOrDefault();
    }

    partial void OnSelectedCommercialListChanged(CommercialPriceList? value)
    {
        if (_suppressCommercialSync) return;
        _suppressCommercialSync = true;
        if (value is null)
        {
            AssignedBooks.Clear();
            AvailableBooksForAssign.Clear();
            CommercialPreviewRows.Clear();
            _suppressCommercialSync = false;
            return;
        }

        EditCurrency = Currencies.FirstOrDefault(c => c.Id == value.CurrencyId) ?? Currencies.FirstOrDefault();
        EditSellUnit = value.SellUnit;
        EditEffectiveDate = value.EffectiveDate.Date;
        UpdateSaleColumnHeader();
        _ = RefreshAssignedBooksAsync();
        _suppressCommercialSync = false;
    }

    private async Task RefreshAssignedBooksAsync()
    {
        if (SelectedCommercialList is null) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var list = await db.CommercialPriceLists
            .Include(x => x.Books).ThenInclude(b => b.PriceBook)
            .FirstOrDefaultAsync(x => x.Id == SelectedCommercialList.Id);
        if (list is null) return;

        AssignedBooks.Clear();
        foreach (var link in list.Books.OrderBy(x => x.SortOrder))
            AssignedBooks.Add(link.PriceBook);

        var assignedIds = await db.CommercialPriceListBooks.Select(x => x.PriceBookId).ToListAsync();
        AvailableBooksForAssign.Clear();
        foreach (var b in await db.PriceBooks.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync())
        {
            if (assignedIds.Contains(b.Id)) continue;
            if (IsVictoryGroupPriceBook(b.Name)) continue;
            AvailableBooksForAssign.Add(b);
        }

        await RefreshCommercialPreviewAsync();
    }

    private static bool IsVictoryGroupPriceBook(string name) =>
        name.Contains("Victory", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Kivu", StringComparison.OrdinalIgnoreCase);

    private async Task RefreshCommercialPreviewAsync()
    {
        CommercialPreviewRows.Clear();
        if (SelectedCommercialList is null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var currency = EditCurrency ?? Currencies.FirstOrDefault();
        var rate = currency?.KesPerUnit ?? 1m;
        var code = currency?.Code ?? "KES";

        foreach (var book in AssignedBooks)
        {
            var rows = await PriceListBriefBuilder.BuildBookRowsInCurrencyAsync(db, book.Id, rate, code);
            foreach (var row in rows)
            {
                var sell = EditSellUnit == PriceUnit.PerBag25Kg ? row.SellBag : row.SellMt;
                CommercialPreviewRows.Add(new CommercialPreviewRow
                {
                    CategoryName = row.CategoryName,
                    FeedTypeName = row.FeedTypeName,
                    SizeName = row.SizeName,
                    CpDisplay = PriceListNumberFormat.FormatNutrient(
                        PriceListNumberFormat.ApplyRound(row.ProteinTarget, RoundCpTo)),
                    FatDisplay = PriceListNumberFormat.FormatNutrient(
                        PriceListNumberFormat.ApplyRound(row.FatTarget, RoundFatTo)),
                    SaleDisplay = PriceListNumberFormat.FormatMoney(
                        PriceListNumberFormat.ApplyRound(sell, RoundSaleTo), code)
                });
            }
        }
    }

    [RelayCommand]
    private async Task AddCommercialListAsync()
    {
        var name = NewCommercialListName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.CommercialPriceLists.AnyAsync(x => x.Name.ToLower() == name.ToLower()))
        {
            StatusMessage = "A commercial list with that name already exists.";
            return;
        }

        var maxOrder = await db.CommercialPriceLists.Select(x => (int?)x.SortOrder).MaxAsync() ?? -1;
        var kes = await db.Currencies.FirstOrDefaultAsync(c => c.IsBase);
        var list = new CommercialPriceList
        {
            Name = name,
            CurrencyId = kes?.Id ?? EditCurrency?.Id,
            SellUnit = PriceUnit.PerMt,
            EffectiveDate = DateTime.Today,
            SortOrder = maxOrder + 1
        };
        db.CommercialPriceLists.Add(list);
        await db.SaveChangesAsync();
        NewCommercialListName = string.Empty;
        StatusMessage = "Commercial price list created.";
        await ReloadCommercialListsAsync(db);
        SelectedCommercialList = CommercialLists.FirstOrDefault(x => x.Id == list.Id);
    }

    public void BeginRenameCommercialList(CommercialPriceList list)
    {
        RenamingListId = list.Id;
        RenameDraftName = list.Name;
    }

    public void CancelRenameCommercialList()
    {
        RenamingListId = null;
        RenameDraftName = string.Empty;
    }

    [RelayCommand]
    private async Task CommitRenameCommercialListAsync()
    {
        if (RenamingListId is not int id) return;

        var name = RenameDraftName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "List name cannot be empty.";
            CancelRenameCommercialList();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.CommercialPriceLists.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower()))
        {
            StatusMessage = "A commercial list with that name already exists.";
            CancelRenameCommercialList();
            return;
        }

        var entity = await db.CommercialPriceLists.FindAsync(id);
        if (entity is null)
        {
            CancelRenameCommercialList();
            return;
        }

        if (string.Equals(entity.Name, name, StringComparison.Ordinal))
        {
            CancelRenameCommercialList();
            return;
        }

        entity.Name = name;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        CancelRenameCommercialList();
        StatusMessage = $"Renamed list to '{name}'.";
        await ReloadCommercialListsAsync(db);
    }

    [RelayCommand]
    private async Task SaveCommercialListAsync()
    {
        if (SelectedCommercialList is null) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.CommercialPriceLists.FindAsync(SelectedCommercialList.Id);
        if (entity is null) return;

        entity.CurrencyId = EditCurrency?.Id;
        entity.SellUnit = EditSellUnit;
        entity.EffectiveDate = EditEffectiveDate.Date;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        StatusMessage = "Commercial list settings saved.";
        await ReloadCommercialListsAsync(db);
        await RefreshCommercialPreviewAsync();
    }

    [RelayCommand]
    private async Task RemoveCommercialListAsync()
    {
        if (SelectedCommercialList is null) return;
        if (MessageBox.Show($"Remove commercial list '{SelectedCommercialList.Name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.CommercialPriceLists.FindAsync(SelectedCommercialList.Id);
        if (entity is null) return;
        db.CommercialPriceLists.Remove(entity);
        await db.SaveChangesAsync();
        StatusMessage = "Commercial list removed.";
        await ReloadCommercialListsAsync(db);
    }

    [RelayCommand]
    private async Task AssignBookAsync()
    {
        if (SelectedCommercialList is null || BookToAssign is null) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.CommercialPriceListBooks.AnyAsync(x => x.PriceBookId == BookToAssign.Id))
        {
            StatusMessage = "That price book is already on a commercial list.";
            return;
        }

        var maxOrder = await db.CommercialPriceListBooks
            .Where(x => x.CommercialPriceListId == SelectedCommercialList.Id)
            .Select(x => (int?)x.SortOrder).MaxAsync() ?? -1;

        db.CommercialPriceListBooks.Add(new CommercialPriceListBook
        {
            CommercialPriceListId = SelectedCommercialList.Id,
            PriceBookId = BookToAssign.Id,
            SortOrder = maxOrder + 1
        });
        await db.SaveChangesAsync();
        BookToAssign = null;
        StatusMessage = "Price book assigned.";
        await RefreshAssignedBooksAsync();
    }

    [RelayCommand]
    private async Task UnassignBookAsync(PriceBook? book)
    {
        if (SelectedCommercialList is null || book is null) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var link = await db.CommercialPriceListBooks.FirstOrDefaultAsync(x =>
            x.CommercialPriceListId == SelectedCommercialList.Id && x.PriceBookId == book.Id);
        if (link is null) return;
        db.CommercialPriceListBooks.Remove(link);
        await db.SaveChangesAsync();
        StatusMessage = "Price book removed from list.";
        await RefreshAssignedBooksAsync();
    }

    [RelayCommand]
    private async Task ReorderCommercialListsAsync(IList<int>? orderedIds)
    {
        if (orderedIds is null || orderedIds.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var lists = await db.CommercialPriceLists.ToListAsync();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var entity = lists.FirstOrDefault(x => x.Id == orderedIds[i]);
            if (entity is null) continue;
            entity.SortOrder = i;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var selectedId = SelectedCommercialList?.Id;
        SuppressCommercialSync = true;
        CommercialLists.Clear();
        foreach (var id in orderedIds)
        {
            var list = lists.FirstOrDefault(x => x.Id == id);
            if (list is not null)
                CommercialLists.Add(list);
        }

        SelectedCommercialList = selectedId is int sid
            ? CommercialLists.FirstOrDefault(x => x.Id == sid) ?? CommercialLists.FirstOrDefault()
            : CommercialLists.FirstOrDefault();
        SuppressCommercialSync = false;
        StatusMessage = "Commercial list order saved.";
    }

    private async Task ReloadVictorySellCompareOptionsAsync(CostWiseDbContext db)
    {
        var snaps = await db.VictoryReportSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        var preferAuto = _pendingCompareSellAuto;
        var preferId = _pendingCompareSnapshotId;
        if (SelectedVictorySellCompare is not null && _pendingCompareSnapshotId is null && !_pendingCompareSellAuto)
        {
            preferAuto = SelectedVictorySellCompare.IsAuto;
            preferId = SelectedVictorySellCompare.SnapshotId;
        }

        VictorySellCompareOptions.Clear();
        VictorySellCompareOptions.Add(VictorySellCompareOption.None);
        VictorySellCompareOptions.Add(VictorySellCompareOption.Auto);
        foreach (var s in snaps)
        {
            VictorySellCompareOptions.Add(new VictorySellCompareOption(
                s.Id,
                isAuto: false,
                $"{s.CreatedAtUtc.ToLocalTime():dd MMM yyyy HH:mm} — {s.Label}"));
        }

        if (preferAuto)
            SelectedVictorySellCompare = VictorySellCompareOption.Auto;
        else if (preferId is int id)
            SelectedVictorySellCompare = VictorySellCompareOptions.FirstOrDefault(x => x.SnapshotId == id)
                                         ?? VictorySellCompareOption.None;
        else
            SelectedVictorySellCompare = VictorySellCompareOption.None;

        _pendingCompareSnapshotId = null;
        _pendingCompareSellAuto = SelectedVictorySellCompare?.IsAuto == true;
        RefreshFilteredVictorySellCompare();
    }

    private async Task<VictoryGroupBrief?> BuildVictoryBriefAsync()
    {
        if (VictoryBookA is null || VictoryBookB is null)
        {
            StatusMessage = "Select both Victory Group price books.";
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var selectedRmIds = RawMaterials.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        var asOf = SelectedVictoryPreviousDate?.DateUtc is DateTime d
            ? d.Date.AddDays(1).AddTicks(-1)
            : (DateTime?)null;
        var rmChanges = await PriceListBriefBuilder.BuildRmChangesAsync(db, selectedRmIds, asOf);
        var rowsA = await PriceListBriefBuilder.BuildBookRowsAsync(db, VictoryBookA.Id);
        var rowsB = await PriceListBriefBuilder.BuildBookRowsAsync(db, VictoryBookB.Id);
        var visible = ColumnToggles.Where(c => c.IsVisible).Select(c => c.Name).ToList();

        var compare = await ResolveCompareSnapshotAsync(db);
        var showCompare = compare is not null;
        if (compare is not null)
        {
            rowsA = VictorySellPriceCompare.Apply(
                rowsA, VictoryBookA.Id, compare.BookAId, VictorySellPriceCompare.RoleA, compare.Lines);
            rowsB = VictorySellPriceCompare.Apply(
                rowsB, VictoryBookB.Id, compare.BookBId, VictorySellPriceCompare.RoleB, compare.Lines);
        }

        return new VictoryGroupBrief(
            FormatVictoryBookTitle(VictoryBookA, rowsA),
            FormatVictoryBookTitle(VictoryBookB, rowsB),
            rowsA,
            rowsB,
            rmChanges,
            Commentary,
            visible,
            BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryLeft),
            BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryRight),
            RoundBookATo,
            RoundBookBTo,
            VictoryBookA.MarginPercent,
            VictoryBookB.MarginPercent,
            compare?.BookAMarginPercent,
            compare?.BookBMarginPercent,
            showCompare,
            RoundDeltaPercentTo);
    }

    private async Task<VictoryReportSnapshot?> ResolveCompareSnapshotAsync(CostWiseDbContext db)
    {
        var opt = SelectedVictorySellCompare;
        if (opt is null || opt == VictorySellCompareOption.None)
            return null;

        if (opt.IsAuto)
        {
            return await db.VictoryReportSnapshots
                .AsNoTracking()
                .Include(x => x.Lines)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync();
        }

        if (opt.SnapshotId is not int id)
            return null;

        return await db.VictoryReportSnapshots
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    private async Task<VictoryReportSnapshot?> SaveVictorySnapshotAsync(
        CostWiseDbContext db,
        VictoryGroupBrief brief,
        string labelNote)
    {
        if (VictoryBookA is null || VictoryBookB is null) return null;

        var now = DateTime.UtcNow;
        var snapshot = new VictoryReportSnapshot
        {
            CreatedAtUtc = now,
            Label = $"{now.ToLocalTime():dd MMM yyyy HH:mm} Victory brief",
            BookAId = VictoryBookA.Id,
            BookBId = VictoryBookB.Id,
            BookAMarginPercent = VictoryBookA.MarginPercent,
            BookBMarginPercent = VictoryBookB.MarginPercent,
            Note = labelNote,
            BriefJson = VictoryBriefArchive.Serialize(brief)
        };

        foreach (var r in brief.BookARows)
        {
            snapshot.Lines.Add(new VictoryReportSnapshotLine
            {
                PriceBookId = VictoryBookA.Id,
                FormulationId = r.FormulationId,
                BookRole = VictorySellPriceCompare.RoleA,
                FormulationCode = r.Code,
                SellMt = r.SellMt,
                SellBag = r.SellBag,
                CurrencyCode = r.CurrencyCode
            });
        }

        foreach (var r in brief.BookBRows)
        {
            snapshot.Lines.Add(new VictoryReportSnapshotLine
            {
                PriceBookId = VictoryBookB.Id,
                FormulationId = r.FormulationId,
                BookRole = VictorySellPriceCompare.RoleB,
                FormulationCode = r.Code,
                SellMt = r.SellMt,
                SellBag = r.SellBag,
                CurrencyCode = r.CurrencyCode
            });
        }

        db.VictoryReportSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        return snapshot;
    }

    private static string FormatVictoryBookTitle(PriceBook book, IReadOnlyList<PriceListBookRow> rows)
    {
        var currency = book.DisplayCurrency?.Code
                       ?? rows.FirstOrDefault()?.CurrencyCode
                       ?? "KES";
        var unit = book.PriceUnit == PriceUnit.PerBag25Kg ? "per bag" : "per MT";
        return $"{book.Name} — {currency} {unit}";
    }

    private async Task<CommercialBrief?> BuildCommercialBriefAsync()
    {
        if (SelectedCommercialList is null)
        {
            StatusMessage = "Select a commercial price list.";
            return null;
        }

        await SaveCommercialListAsync();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var currency = EditCurrency ?? await db.Currencies.FirstOrDefaultAsync(c => c.IsBase);
        var rate = currency?.KesPerUnit ?? 1m;
        var code = currency?.Code ?? "KES";

        var allRows = new List<PriceListBookRow>();
        foreach (var book in AssignedBooks)
        {
            var rows = await PriceListBriefBuilder.BuildBookRowsInCurrencyAsync(db, book.Id, rate, code);
            allRows.AddRange(rows);
        }

        var transportIncluded = AssignedBooks.Any(b => b.TransportationCost > 0m);

        return new CommercialBrief(
            SelectedCommercialList.Name,
            EditEffectiveDate.Date,
            code,
            EditSellUnit,
            allRows,
            BrandingLogoStore.GetBytes(BrandingLogoStore.Commercial),
            GetCommercialVisibleColumns(),
            RoundSaleTo,
            RoundCpTo,
            RoundFatTo,
            transportIncluded);
    }

    [RelayCommand]
    private async Task DownloadVictoryPdfAsync()
    {
        try
        {
            var brief = await BuildVictoryBriefAsync();
            if (brief is null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Victory-Group-Pricing-{DateTime.Now:yyyyMMdd}.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            VictoryGroupPriceListWriter.WritePdf(brief, dialog.FileName);

            await using var db = await _dbFactory.CreateDbContextAsync();
            await SaveVictorySnapshotAsync(db, brief, "Download PDF");
            await ReloadVictorySellCompareOptionsAsync(db);
            await ReloadSavedBriefsAsync(db);

            PersistVictoryPrefs();
            StatusMessage = $"Saved {dialog.FileName} (sell-price snapshot recorded).";
            ScheduleVictoryPreviewRefresh();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "PDF export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveVictorySellSnapshotAsync()
    {
        try
        {
            var brief = await BuildVictoryBriefAsync();
            if (brief is null) return;

            // Snapshot current live sells without compare overlay values on lines
            await using var db = await _dbFactory.CreateDbContextAsync();
            var liveA = await PriceListBriefBuilder.BuildBookRowsAsync(db, VictoryBookA!.Id);
            var liveB = await PriceListBriefBuilder.BuildBookRowsAsync(db, VictoryBookB!.Id);
            var snapBrief = brief with { BookARows = liveA, BookBRows = liveB, ShowSellCompare = false };
            await SaveVictorySnapshotAsync(db, snapBrief, "Manual snapshot");
            await ReloadVictorySellCompareOptionsAsync(db);
            await ReloadSavedBriefsAsync(db);
            PersistVictoryPrefs();
            StatusMessage = "Victory sell-price snapshot saved.";
            ScheduleVictoryPreviewRefresh();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Snapshot failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DownloadCommercialPdfAsync()
    {
        try
        {
            var brief = await BuildCommercialBriefAsync();
            if (brief is null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Commercial-{SanitizeFile(brief.ListName)}-{brief.EffectiveDate:yyyyMMdd}.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            CommercialPriceListWriter.WritePdf(brief, dialog.FileName);
            StatusMessage = $"Saved {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "PDF export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ViewCommercialPdfAsync()
    {
        try
        {
            var brief = await BuildCommercialBriefAsync();
            if (brief is null) return;

            var path = TempPdfPath($"Commercial-{SanitizeFile(brief.ListName)}");
            CommercialPriceListWriter.WritePdf(brief, path);
            OpenPdf(path);
            StatusMessage = "Opened commercial PDF preview.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "PDF preview failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public string SavedBriefPreviewZoomLabel => $"{SavedBriefPreviewZoom * 100:0}%";

    partial void OnSavedBriefPreviewZoomChanged(double value) =>
        OnPropertyChanged(nameof(SavedBriefPreviewZoomLabel));

    partial void OnSavedBriefFilterChanged(string value) => RefreshFilteredSavedBriefs();

    partial void OnSelectedSavedBriefChanged(SavedBriefItem? value) => ScheduleSavedBriefPreview();

    [RelayCommand]
    private void ZoomSavedBriefPreviewIn() =>
        SavedBriefPreviewZoom = Math.Min(3.0, Math.Round(SavedBriefPreviewZoom + 0.1, 2));

    [RelayCommand]
    private void ZoomSavedBriefPreviewOut() =>
        SavedBriefPreviewZoom = Math.Max(0.5, Math.Round(SavedBriefPreviewZoom - 0.1, 2));

    public void AdjustSavedBriefPreviewZoom(int deltaSteps)
    {
        if (deltaSteps == 0) return;
        var next = SavedBriefPreviewZoom + deltaSteps * 0.1;
        SavedBriefPreviewZoom = Math.Clamp(Math.Round(next, 2), 0.5, 3.0);
    }

    private async Task ReloadSavedBriefsAsync(CostWiseDbContext db)
    {
        var selectedId = SelectedSavedBrief?.Id;
        var rows = await db.VictoryReportSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new SavedBriefItem
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                Label = x.Label,
                Note = x.Note,
                BookAName = x.BookA != null ? x.BookA.Name : "Price book A",
                BookBName = x.BookB != null ? x.BookB.Name : "Price book B",
                HasBrief = x.BriefJson != null && x.BriefJson != ""
            })
            .ToListAsync();

        SavedBriefs.Clear();
        foreach (var row in rows)
            SavedBriefs.Add(row);

        RefreshFilteredSavedBriefs();
        SelectedSavedBrief = FilteredSavedBriefs.FirstOrDefault(x => x.Id == selectedId)
                             ?? FilteredSavedBriefs.FirstOrDefault();
    }

    private void RefreshFilteredSavedBriefs()
    {
        var q = SavedBriefFilter.Trim();
        var selectedId = SelectedSavedBrief?.Id;
        FilteredSavedBriefs.Clear();
        foreach (var item in SavedBriefs)
        {
            if (q.Length == 0
                || item.WhenLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
                || item.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
                || item.BookSummary.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (item.Note?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                FilteredSavedBriefs.Add(item);
            }
        }

        if (selectedId is int id && FilteredSavedBriefs.All(x => x.Id != id))
            SelectedSavedBrief = FilteredSavedBriefs.FirstOrDefault();
        else if (selectedId is int keep)
            SelectedSavedBrief = FilteredSavedBriefs.FirstOrDefault(x => x.Id == keep);
    }

    private void ScheduleSavedBriefPreview()
    {
        _savedBriefCts?.Cancel();
        _savedBriefCts?.Dispose();
        _savedBriefCts = new CancellationTokenSource();
        var token = _savedBriefCts.Token;
        _ = LoadSavedBriefPreviewAsync(token);
    }

    private async Task LoadSavedBriefPreviewAsync(CancellationToken token)
    {
        var item = SelectedSavedBrief;
        if (item is null)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                SavedBriefPreviewPages.Clear();
                SavedBriefLines.Clear();
                SavedBriefShowsPreview = false;
                SavedBriefShowsLines = false;
                SavedBriefPreviewStatus = "No saved briefs yet.";
            });
            return;
        }

        try
        {
            SavedBriefPreviewStatus = "Opening brief…";
            await using var db = await _dbFactory.CreateDbContextAsync(token);
            var snap = await db.VictoryReportSnapshots
                .AsNoTracking()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == item.Id, token);
            if (token.IsCancellationRequested)
                return;

            VictoryGroupBrief? brief = snap is null
                ? null
                : VictoryBriefArchive.Deserialize(
                    snap.BriefJson,
                    BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryLeft),
                    BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryRight));

            IReadOnlyList<byte[]>? pages = null;
            if (brief is not null)
            {
                pages = await Task.Run(
                    () => VictoryGroupPriceListWriter.RenderPreviewImages(brief),
                    token);
            }

            if (token.IsCancellationRequested)
                return;

            var lineRows = (snap?.Lines ?? [])
                .OrderBy(l => l.BookRole)
                .ThenBy(l => l.FormulationCode)
                .Select(l => new SavedBriefLineRow
                {
                    BookSide = l.BookRole,
                    FormulationCode = string.IsNullOrWhiteSpace(l.FormulationCode) ? "—" : l.FormulationCode,
                    SellMtDisplay = PriceListNumberFormat.FormatMoney(l.SellMt, l.CurrencyCode),
                    SellBagDisplay = PriceListNumberFormat.FormatMoney(l.SellBag, l.CurrencyCode),
                    CurrencyCode = l.CurrencyCode
                })
                .ToList();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                SavedBriefPreviewPages.Clear();
                if (pages is not null)
                {
                    foreach (var png in pages)
                        SavedBriefPreviewPages.Add(ToImageSource(png));
                }

                SavedBriefLines.Clear();
                foreach (var row in lineRows)
                    SavedBriefLines.Add(row);

                SavedBriefShowsPreview = SavedBriefPreviewPages.Count > 0;
                SavedBriefShowsLines = !SavedBriefShowsPreview && SavedBriefLines.Count > 0;
                SavedBriefPreviewStatus = snap is null
                    ? "Snapshot was not found."
                    : SavedBriefShowsPreview
                        ? string.Empty
                        : "The original brief image was not stored. Showing saved sell prices.";
            });
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer selection
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (Application.Current?.Dispatcher is null)
            {
                SavedBriefPreviewStatus = message;
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() => SavedBriefPreviewStatus = message);
        }
    }

    [RelayCommand]
    private void UseSavedBriefForLastPrice()
    {
        if (SelectedSavedBrief is null)
            return;

        var item = SelectedSavedBrief;
        var existing = VictorySellCompareOptions.FirstOrDefault(x => x.SnapshotId == item.Id);
        if (existing is null)
        {
            existing = new VictorySellCompareOption(item.Id, isAuto: false, item.CompareLabel);
            VictorySellCompareOptions.Insert(Math.Min(2, VictorySellCompareOptions.Count), existing);
            RefreshFilteredVictorySellCompare();
        }

        SelectedVictorySellCompare = existing;
        SelectedPriceListTab = 0;
        StatusMessage = $"Comparing sell prices to {item.WhenLabel}.";
    }

    [RelayCommand]
    private async Task DownloadSavedBriefPdfAsync()
    {
        if (SelectedSavedBrief is null)
            return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var json = await db.VictoryReportSnapshots
                .AsNoTracking()
                .Where(x => x.Id == SelectedSavedBrief.Id)
                .Select(x => x.BriefJson)
                .FirstOrDefaultAsync();
            var brief = VictoryBriefArchive.Deserialize(
                json,
                BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryLeft),
                BrandingLogoStore.GetBytes(BrandingLogoStore.VictoryRight));
            if (brief is null)
            {
                StatusMessage = "The full brief was not stored for this snapshot.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Victory-Group-Pricing-{SelectedSavedBrief.CreatedAtUtc.ToLocalTime():yyyyMMdd}.pdf"
            };
            if (dialog.ShowDialog() != true)
                return;

            VictoryGroupPriceListWriter.WritePdf(brief, dialog.FileName);
            StatusMessage = $"Saved {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "PDF export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteSavedBriefAsync()
    {
        if (SelectedSavedBrief is null)
            return;

        var item = SelectedSavedBrief;
        var confirm = MessageBox.Show(
            $"Delete the brief from {item.WhenLabel}?",
            "Delete saved brief",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var entity = await db.VictoryReportSnapshots.FirstOrDefaultAsync(x => x.Id == item.Id);
            if (entity is not null)
            {
                db.VictoryReportSnapshots.Remove(entity);
                await db.SaveChangesAsync();
            }

            await ReloadSavedBriefsAsync(db);
            await ReloadVictorySellCompareOptionsAsync(db);
            StatusMessage = "Saved brief deleted.";
            ScheduleVictoryPreviewRefresh();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string TempPdfPath(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), "CostWise");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
    }

    private static void OpenPdf(string path) =>
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

    private static string SanitizeFile(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '-');
        return name;
    }
}
