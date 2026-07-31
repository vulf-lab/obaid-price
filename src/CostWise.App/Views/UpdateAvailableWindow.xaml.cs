using System.Windows;
using CostWise.App.Services.Update;

namespace CostWise.App.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdatePromptResult Result { get; private set; } = UpdatePromptResult.Later;

    public UpdateAvailableWindow(string currentVersion, string newVersion, string releaseNotes)
    {
        InitializeComponent();
        Title = "Update available";
        CurrentVersionText.Text = $"Current version: {currentVersion}";
        NewVersionText.Text = $"New version: {newVersion}";
        NotesText.Text = string.IsNullOrWhiteSpace(releaseNotes)
            ? "No release notes provided."
            : releaseNotes;
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.UpdateNow;
        DialogResult = true;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.Later;
        DialogResult = false;
        Close();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Result = UpdatePromptResult.SkipVersion;
        DialogResult = true;
        Close();
    }
}
