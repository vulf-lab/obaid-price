using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using CostWise.App.ViewModels;
using CostWise.Core.Entities;

namespace CostWise.App.Views;

public partial class PriceListsView : UserControl
{
    private PriceListsViewModel? _subscribedVm;
    private Point _listDragStart;
    private CommercialPriceList? _listDragItem;
    private bool _suppressRenameCommit;

    public PriceListsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ApplyCommercialColumnState();
    }

    private void VictoryPreviewScroller_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (DataContext is not PriceListsViewModel vm) return;

        vm.AdjustVictoryPreviewZoom(e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    private void VictoryColumnsButton_OnClick(object sender, RoutedEventArgs e) =>
        VictoryColumnsPopup.IsOpen = !VictoryColumnsPopup.IsOpen;

    private void CommercialColumnsButton_OnClick(object sender, RoutedEventArgs e) =>
        CommercialColumnsPopup.IsOpen = !CommercialColumnsPopup.IsOpen;

    private void VictoryPreviousDateButton_OnClick(object sender, RoutedEventArgs e) =>
        VictoryPreviousDatePopup.IsOpen = !VictoryPreviousDatePopup.IsOpen;

    private void VictorySellCompareButton_OnClick(object sender, RoutedEventArgs e) =>
        VictorySellComparePopup.IsOpen = !VictorySellComparePopup.IsOpen;

    private void VictoryPreviousDatePopup_OnOpened(object sender, EventArgs e)
    {
        if (DataContext is PriceListsViewModel vm)
            vm.ClearVictoryPreviousDateFilter();
        VictoryPreviousDateSearchBox.Focus();
    }

    private void VictoryPreviousDatePopup_OnClosed(object sender, EventArgs e)
    {
        if (DataContext is PriceListsViewModel vm)
            vm.ClearVictoryPreviousDateFilter();
    }

    private void VictorySellComparePopup_OnOpened(object sender, EventArgs e)
    {
        if (DataContext is PriceListsViewModel vm)
            vm.ClearVictorySellCompareFilter();
        VictorySellCompareSearchBox.Focus();
    }

    private void VictorySellComparePopup_OnClosed(object sender, EventArgs e)
    {
        if (DataContext is PriceListsViewModel vm)
            vm.ClearVictorySellCompareFilter();
    }

    private void VictoryPreviousDateList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!VictoryPreviousDatePopup.IsOpen) return;
        if (e.AddedItems.Count == 0) return;
        VictoryPreviousDatePopup.IsOpen = false;
    }

    private void VictorySellCompareList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!VictorySellComparePopup.IsOpen) return;
        if (e.AddedItems.Count == 0) return;
        VictorySellComparePopup.IsOpen = false;
    }

    private void CommercialListsBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _listDragItem = null;
            return;
        }

        _listDragStart = e.GetPosition(null);
        _listDragItem = GetListBoxItemData(e.OriginalSource as DependencyObject) as CommercialPriceList;
    }

    private void CommercialListName_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if (DataContext is not PriceListsViewModel vm) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not CommercialPriceList list)
            return;

        vm.BeginRenameCommercialList(list);
        e.Handled = true;
    }

    private void CommercialListRenameBox_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Visibility != Visibility.Visible) return;
        box.Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            box.SelectAll();
        });
    }

    private void CommercialListRenameBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressRenameCommit) return;
        if (DataContext is not PriceListsViewModel vm) return;
        if (vm.RenamingListId is null) return;
        if (vm.CommitRenameCommercialListCommand.CanExecute(null))
            vm.CommitRenameCommercialListCommand.Execute(null);
    }

    private void CommercialListRenameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PriceListsViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (vm.CommitRenameCommercialListCommand.CanExecute(null))
                vm.CommitRenameCommercialListCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _suppressRenameCommit = true;
            vm.CancelRenameCommercialList();
            _suppressRenameCommit = false;
        }
    }

    private void CommercialListsBox_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _listDragItem is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _listDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _listDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(CommercialListsBox, _listDragItem, DragDropEffects.Move);
        _listDragItem = null;
    }

    private void CommercialListsBox_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CommercialPriceList))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void CommercialListsBox_OnDrop(object sender, DragEventArgs e)
    {
        if (_subscribedVm is null) return;
        if (e.Data.GetData(typeof(CommercialPriceList)) is not CommercialPriceList source)
            return;

        var target = GetListBoxItemData(e.OriginalSource as DependencyObject) as CommercialPriceList;
        var sourceIndex = _subscribedVm.CommercialLists.IndexOf(source);
        if (sourceIndex < 0) return;

        var targetIndex = target is null
            ? _subscribedVm.CommercialLists.Count - 1
            : _subscribedVm.CommercialLists.IndexOf(target);
        if (targetIndex < 0 || sourceIndex == targetIndex) return;

        _subscribedVm.CommercialLists.Move(sourceIndex, targetIndex);
        var orderedIds = _subscribedVm.CommercialLists.Select(x => x.Id).ToList();
        if (_subscribedVm.ReorderCommercialListsCommand.CanExecute(orderedIds))
            _subscribedVm.ReorderCommercialListsCommand.Execute(orderedIds);

        e.Handled = true;
    }

    private static object? GetListBoxItemData(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListBoxItem item)
                return item.DataContext;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = e.NewValue as PriceListsViewModel;
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        ApplyCommercialColumnState();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(PriceListsViewModel.ShowCommercialCategory)
            or nameof(PriceListsViewModel.ShowCommercialFeedType)
            or nameof(PriceListsViewModel.ShowCommercialSize)
            or nameof(PriceListsViewModel.ShowCommercialCp)
            or nameof(PriceListsViewModel.ShowCommercialFat)
            or nameof(PriceListsViewModel.ShowCommercialSale)
            or nameof(PriceListsViewModel.SaleColumnHeader))
        {
            ApplyCommercialColumnState();
        }
    }

    private void ApplyCommercialColumnState()
    {
        if (CommercialPreviewGrid is null || DataContext is not PriceListsViewModel vm)
            return;

        var cols = CommercialPreviewGrid.Columns;
        if (cols.Count < 6) return;

        cols[0].Visibility = vm.ShowCommercialCategory ? Visibility.Visible : Visibility.Collapsed;
        cols[1].Visibility = vm.ShowCommercialFeedType ? Visibility.Visible : Visibility.Collapsed;
        cols[2].Visibility = vm.ShowCommercialSize ? Visibility.Visible : Visibility.Collapsed;
        cols[3].Visibility = vm.ShowCommercialCp ? Visibility.Visible : Visibility.Collapsed;
        cols[4].Visibility = vm.ShowCommercialFat ? Visibility.Visible : Visibility.Collapsed;
        cols[5].Visibility = vm.ShowCommercialSale ? Visibility.Visible : Visibility.Collapsed;
        cols[5].Header = vm.SaleColumnHeader;
    }
}
