using System.Windows;
using System.Windows.Input;
using CostWise.App.ViewModels;

namespace CostWise.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => FocusPrimaryField();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LoginViewModel.IsSetupMode)
                or nameof(LoginViewModel.IsPinMode)
                or nameof(LoginViewModel.IsPasswordMode)
                or nameof(LoginViewModel.IsDamagedMode))
                Dispatcher.BeginInvoke(FocusPrimaryField);
        };
    }

    private LoginViewModel Vm => (LoginViewModel)DataContext;

    private void FocusPrimaryField()
    {
        if (Vm.IsSetupMode)
            SetupPasswordBox.Focus();
        else if (Vm.IsPinMode)
            PinBox.Focus();
        else if (Vm.IsPasswordMode)
            PasswordBox.Focus();
    }

    private void SignIn_OnClick(object sender, RoutedEventArgs e) => AttemptPassword();

    private void Unlock_OnClick(object sender, RoutedEventArgs e) => AttemptPin();

    private void Setup_OnClick(object sender, RoutedEventArgs e) => AttemptSetup();

    private void CredentialBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (Vm.IsSetupMode)
            AttemptSetup();
        else if (Vm.IsPinMode)
            AttemptPin();
        else if (Vm.IsPasswordMode)
            AttemptPassword();
    }

    private void AttemptSetup()
    {
        if (!Vm.TryCompleteSetup(SetupPasswordBox.Password, SetupConfirmBox.Password))
        {
            SetupPasswordBox.Clear();
            SetupConfirmBox.Clear();
            SetupPasswordBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void AttemptPassword()
    {
        if (!Vm.TryPasswordLogin(PasswordBox.Password))
        {
            PasswordBox.Clear();
            PasswordBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void AttemptPin()
    {
        if (!Vm.TryPinUnlock(PinBox.Password))
        {
            PinBox.Clear();
            PinBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        DialogResult = false;
    }
}
