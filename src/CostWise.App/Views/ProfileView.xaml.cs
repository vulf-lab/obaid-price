using System.Windows;
using System.Windows.Controls;
using CostWise.App.ViewModels;

namespace CostWise.App.Views;

public partial class ProfileView : UserControl
{
    public ProfileView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ProfileViewModel vm)
                vm.RefreshFromStore();
        };
    }

    private ProfileViewModel? Vm => DataContext as ProfileViewModel;

    private void UpdatePassword_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Vm.TryChangePassword(
                CurrentPasswordBox.Password,
                NewPasswordBox.Password,
                ConfirmPasswordBox.Password,
                out var message))
        {
            CurrentPasswordBox.Clear();
            NewPasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }

        Vm.StatusMessage = message;
    }

    private void SavePin_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Vm.TrySavePin(
                PinCurrentPasswordBox.Password,
                NewPinBox.Password,
                ConfirmPinBox.Password,
                out var message))
        {
            PinCurrentPasswordBox.Clear();
            NewPinBox.Clear();
            ConfirmPinBox.Clear();
        }

        Vm.StatusMessage = message;
    }

    private void ClearPin_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (Vm.TryClearPin(PinCurrentPasswordBox.Password, out var message))
        {
            PinCurrentPasswordBox.Clear();
            NewPinBox.Clear();
            ConfirmPinBox.Clear();
        }

        Vm.StatusMessage = message;
    }
}
