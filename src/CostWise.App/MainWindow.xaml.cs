using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CostWise.App.ViewModels;

namespace CostWise.App;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private Point _dragStart;
    private object? _dragItem;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
        viewModel.Initialize();
    }

    private void NavList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = GetListBoxItemData(e.OriginalSource as DependencyObject);
    }

    private void NavList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(NavList, _dragItem, DragDropEffects.Move);
        _dragItem = null;
    }

    private void NavList_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(NavItem))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void NavList_OnDrop(object sender, DragEventArgs e)
    {
        if (_vm is null) return;
        if (e.Data.GetData(typeof(NavItem)) is not NavItem source)
            return;

        var target = GetListBoxItemData(e.OriginalSource as DependencyObject) as NavItem;
        var sourceIndex = _vm.NavItems.IndexOf(source);
        if (sourceIndex < 0) return;

        var targetIndex = target is null
            ? _vm.NavItems.Count - 1
            : _vm.NavItems.IndexOf(target);
        if (targetIndex < 0 || sourceIndex == targetIndex) return;

        _vm.NavItems.Move(sourceIndex, targetIndex);
        _vm.PersistNavOrder();
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
}
