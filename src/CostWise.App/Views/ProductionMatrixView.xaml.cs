using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CostWise.App.ViewModels;
using CostWise.Core.Entities;

namespace CostWise.App.Views;

public partial class ProductionMatrixView
{
    private ProductionMatrixViewModel? _vm;
    private Point _dragStart;
    private object? _dragItem;

    public ProductionMatrixView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachVm();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        if (e.NewValue is ProductionMatrixViewModel vm)
        {
            _vm = vm;
            vm.MatrixStructureChanged += RebuildColumns;
            RebuildColumns();
        }
    }

    private void DetachVm()
    {
        if (_vm is null) return;
        _vm.MatrixStructureChanged -= RebuildColumns;
        _vm = null;
    }

    private void GroupNameBox_OnLostFocus(object sender, RoutedEventArgs e) =>
        CommitGroupName();

    private void GroupNameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitGroupName();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitGroupName()
    {
        if (_vm?.SaveGroupNameCommand.CanExecute(null) == true)
            _vm.SaveGroupNameCommand.Execute(null);
    }

    private void GroupsList_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = GetListBoxItemData(e.OriginalSource as DependencyObject);
    }

    private void GroupsList_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
            return;

        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        DragDrop.DoDragDrop(GroupsList, _dragItem, DragDropEffects.Move);
        _dragItem = null;
    }

    private void GroupsList_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ProductionGroup))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void GroupsList_OnDrop(object sender, DragEventArgs e)
    {
        if (_vm is null) return;
        if (e.Data.GetData(typeof(ProductionGroup)) is not ProductionGroup source)
            return;

        var target = GetListBoxItemData(e.OriginalSource as DependencyObject) as ProductionGroup;
        var sourceIndex = _vm.Groups.IndexOf(source);
        if (sourceIndex < 0) return;

        var targetIndex = target is null
            ? _vm.Groups.Count - 1
            : _vm.Groups.IndexOf(target);
        if (targetIndex < 0 || sourceIndex == targetIndex) return;

        _vm.Groups.Move(sourceIndex, targetIndex);
        var orderedIds = _vm.Groups.Select(g => g.Id).ToList();
        if (_vm.ReorderGroupsCommand.CanExecute(orderedIds))
            _vm.ReorderGroupsCommand.Execute(orderedIds);

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

    private void RebuildColumns()
    {
        if (_vm is null) return;

        MatrixGrid.Columns.Clear();

        var ingredientCol = new DataGridTextColumn
        {
            Header = "Ingredient",
            Binding = new Binding(nameof(ProductionMatrixRow.IngredientName)),
            IsReadOnly = true,
            MinWidth = 180,
            Width = new DataGridLength(220)
        };
        MatrixGrid.Columns.Add(ingredientCol);

        for (var i = 0; i < _vm.Columns.Count; i++)
        {
            var colMeta = _vm.Columns[i];
            var index = i;

            var header = new StackPanel { Margin = new Thickness(4, 2, 4, 2) };
            header.Children.Add(new TextBlock
            {
                Text = colMeta.CategoryName,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            header.Children.Add(new TextBlock
            {
                Text = colMeta.SizeName,
                TextAlignment = TextAlignment.Center,
                Foreground = (Brush)FindResource("TextMutedBrush")
            });
            header.Children.Add(new TextBlock
            {
                Text = colMeta.Code,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });

            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"Cells[{index}].PercentText")
                {
                    Mode = BindingMode.OneWay
                },
                IsReadOnly = true,
                Width = new DataGridLength(100),
                MinWidth = 80,
                ElementStyle = CreateCellStyle(colMeta)
            };
            MatrixGrid.Columns.Add(column);
        }
    }

    private static Style CreateCellStyle(ProductionMatrixColumn colMeta)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Triggers.Add(new DataTrigger
        {
            Binding = new Binding(nameof(ProductionMatrixColumn.IsValid)) { Source = colMeta },
            Value = false,
            Setters =
            {
                new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xB3, 0x3A, 0x3A)))
            }
        });
        return style;
    }
}
