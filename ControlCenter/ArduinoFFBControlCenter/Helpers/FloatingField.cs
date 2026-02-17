using System.Windows;
using System.Windows.Controls;

namespace ArduinoFFBControlCenter.Helpers;

/// <summary>
/// Attached properties used by floating label input templates.
/// Keeps field state logic out of viewmodels and code-behind.
/// </summary>
public static class FloatingField
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.RegisterAttached(
            "Label",
            typeof(string),
            typeof(FloatingField),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    private static readonly DependencyPropertyKey IsFilledPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "IsFilled",
            typeof(bool),
            typeof(FloatingField),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsFilledProperty = IsFilledPropertyKey.DependencyProperty;

    public static string GetLabel(DependencyObject obj) => (string)obj.GetValue(LabelProperty);
    public static void SetLabel(DependencyObject obj, string value) => obj.SetValue(LabelProperty, value);

    public static bool GetIsFilled(DependencyObject obj) => (bool)obj.GetValue(IsFilledProperty);
    private static void SetIsFilled(DependencyObject obj, bool value) => obj.SetValue(IsFilledPropertyKey, value);

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        switch (d)
        {
            case TextBox textBox:
                textBox.Loaded -= TextBoxOnLoaded;
                textBox.TextChanged -= TextBoxOnTextChanged;
                textBox.Loaded += TextBoxOnLoaded;
                textBox.TextChanged += TextBoxOnTextChanged;
                UpdateTextBox(textBox);
                break;

            case ComboBox comboBox:
                comboBox.Loaded -= ComboBoxOnLoaded;
                comboBox.SelectionChanged -= ComboBoxOnSelectionChanged;
                comboBox.Loaded += ComboBoxOnLoaded;
                comboBox.SelectionChanged += ComboBoxOnSelectionChanged;
                UpdateComboBox(comboBox);
                break;
        }
    }

    private static void TextBoxOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateTextBox(textBox);
        }
    }

    private static void TextBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            UpdateTextBox(textBox);
        }
    }

    private static void ComboBoxOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateComboBox(comboBox);
        }
    }

    private static void ComboBoxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            UpdateComboBox(comboBox);
        }
    }

    private static void UpdateTextBox(TextBox textBox)
    {
        SetIsFilled(textBox, !string.IsNullOrWhiteSpace(textBox.Text));
    }

    private static void UpdateComboBox(ComboBox comboBox)
    {
        var isFilled = comboBox.SelectedItem != null || !string.IsNullOrWhiteSpace(comboBox.Text);
        SetIsFilled(comboBox, isFilled);
    }
}
