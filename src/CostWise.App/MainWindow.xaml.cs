using System.Windows;
using CostWise.App.ViewModels;

namespace CostWise.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Initialize();
    }
}
