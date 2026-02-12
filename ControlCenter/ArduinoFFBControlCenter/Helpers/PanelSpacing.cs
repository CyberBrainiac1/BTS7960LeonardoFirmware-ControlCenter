using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ArduinoFFBControlCenter.Helpers;

public static class PanelSpacing
{
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.RegisterAttached(
        "Spacing",
        typeof(double),
        typeof(PanelSpacing),
        new PropertyMetadata(0d, OnSpacingChanged));

    private static readonly DependencyProperty OriginalMarginProperty = DependencyProperty.RegisterAttached(
        "OriginalMargin",
        typeof(Thickness),
        typeof(PanelSpacing),
        new PropertyMetadata(new Thickness()));

    public static double GetSpacing(DependencyObject obj) => (double)obj.GetValue(SpacingProperty);
    public static void SetSpacing(DependencyObject obj, double value) => obj.SetValue(SpacingProperty, value);

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Panel panel)
        {
            return;
        }

        if ((double)e.NewValue > 0)
        {
            panel.Loaded -= OnPanelLoaded;
            panel.Loaded += OnPanelLoaded;
            panel.Unloaded -= OnPanelUnloaded;
            panel.Unloaded += OnPanelUnloaded;

            if (panel.Children is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged -= OnChildrenChanged;
                notify.CollectionChanged += OnChildrenChanged;
            }
        }

        UpdateSpacing(panel);
    }

    private static void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel panel)
        {
            UpdateSpacing(panel);
        }
    }

    private static void OnPanelUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel panel && panel.Children is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged -= OnChildrenChanged;
        }
    }

    private static void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is Panel panel)
        {
            UpdateSpacing(panel);
        }
    }

    private static void UpdateSpacing(Panel panel)
    {
        if (panel is not StackPanel stack)
        {
            return;
        }

        var spacing = GetSpacing(stack);
        if (spacing <= 0)
        {
            return;
        }

        var index = 0;
        foreach (var child in stack.Children.OfType<FrameworkElement>())
        {
            var baseMargin = child.ReadLocalValue(OriginalMarginProperty) is Thickness stored
                ? stored
                : child.Margin;

            if (child.ReadLocalValue(OriginalMarginProperty) == DependencyProperty.UnsetValue)
            {
                child.SetValue(OriginalMarginProperty, baseMargin);
            }

            if (index == 0)
            {
                child.Margin = baseMargin;
            }
            else if (stack.Orientation == Orientation.Vertical)
            {
                child.Margin = new Thickness(baseMargin.Left, baseMargin.Top + spacing, baseMargin.Right, baseMargin.Bottom);
            }
            else
            {
                child.Margin = new Thickness(baseMargin.Left + spacing, baseMargin.Top, baseMargin.Right, baseMargin.Bottom);
            }

            index++;
        }
    }
}
