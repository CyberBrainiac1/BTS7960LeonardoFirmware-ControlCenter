using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArduinoFFBControlCenter.Views;

public partial class FirstRunProfileWindow : Window
{
    private static readonly Regex BasicEmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public FirstRunProfileWindow(string? currentName = null, string? currentEmail = null)
    {
        InitializeComponent();
        DataContext = new FirstRunProfileState
        {
            UserName = currentName ?? string.Empty,
            UserEmail = currentEmail ?? string.Empty,
            Provider = "Ollama",
            Endpoint = "http://localhost:11434"
        };

        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public string UserName => (DataContext as FirstRunProfileState)?.UserName?.Trim() ?? string.Empty;
    public string UserEmail => (DataContext as FirstRunProfileState)?.UserEmail?.Trim() ?? string.Empty;
    public string Provider => (DataContext as FirstRunProfileState)?.Provider ?? "Ollama";
    public string Endpoint => (DataContext as FirstRunProfileState)?.Endpoint?.Trim() ?? "http://localhost:11434";
    public string ApiKey => (DataContext as FirstRunProfileState)?.ApiKey?.Trim() ?? string.Empty;

    private void OnContinueClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            MessageBox.Show(this, "Enter your name.", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(UserEmail) || !BasicEmailRegex.IsMatch(UserEmail))
        {
            MessageBox.Show(this, "Enter a valid email address.", "Profile", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }

    private sealed partial class FirstRunProfileState : ObservableObject
    {
        [ObservableProperty] private string userName = string.Empty;
        [ObservableProperty] private string userEmail = string.Empty;
        [ObservableProperty] private string provider = "Ollama";
        [ObservableProperty] private string endpoint = "http://localhost:11434";
        [ObservableProperty] private string apiKey = string.Empty;

        public IReadOnlyList<string> Providers { get; } = new[] { "Ollama", "ApiKey" };
    }
}
