using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace RelicLauncher.App.Views.Controls;

public partial class RelicComboBox : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<RelicComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<RelicComboBox, object?>(
            nameof(SelectedItem),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<RelicComboBox, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<RelicComboBox, string?>(nameof(PlaceholderText));

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<RelicComboBox, bool>(nameof(IsDropDownOpen));

    public static readonly StyledProperty<double> MaxDropDownHeightProperty =
        AvaloniaProperty.Register<RelicComboBox, double>(nameof(MaxDropDownHeight), 280);

    private static readonly FuncDataTemplate<object?> DefaultItemTemplate = new((item, scope) =>
    {
        var block = new TextBlock
        {
            Text = GetItemDisplayText(item),
            FontSize = 14,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (scope is StyledElement element
            && element.TryFindResource("Theme.Text", out var brush)
            && brush is IBrush foreground)
        {
            block.Foreground = foreground;
        }

        return block;
    });

    private Border? _chrome;
    private TextBlock? _selectedText;
    private TextBlock? _placeholderText;
    private ListBox? _itemsList;

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public double MaxDropDownHeight
    {
        get => GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    static RelicComboBox()
    {
        IsEnabledProperty.Changed.AddClassHandler<RelicComboBox>((combo, _) => combo.UpdateChromeState());
        SelectedItemProperty.Changed.AddClassHandler<RelicComboBox>((combo, _) => combo.UpdateSelectedText());
        ItemTemplateProperty.Changed.AddClassHandler<RelicComboBox>((combo, _) => combo.UpdateListItemTemplate());
    }

    public RelicComboBox()
    {
        InitializeComponent();
        _chrome = this.FindControl<Border>("Chrome");
        _selectedText = this.FindControl<TextBlock>("SelectedText");
        _placeholderText = this.FindControl<TextBlock>("PlaceholderTextBlock");
        _itemsList = this.FindControl<ListBox>("ItemsList");
        UpdateChromeState();
        UpdateListItemTemplate();
        UpdateSelectedText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsEnabledProperty)
        {
            UpdateChromeState();
        }
    }

    internal static string GetItemDisplayText(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (item is string text)
        {
            return text;
        }

        var type = item.GetType();
        foreach (var propertyName in new[] { "Label", "DisplayName", "DisplayLabel", "Name" })
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(item) is string value && value.Length > 0)
            {
                return value;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    private void OnChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            IsDropDownOpen = !IsDropDownOpen;
            e.Handled = true;
        }
    }

    private void OnItemsListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            IsDropDownOpen = false;
        }
    }

    private void UpdateChromeState()
    {
        if (_chrome is null)
        {
            return;
        }

        _chrome.Opacity = IsEnabled ? 1 : 0.55;
        _chrome.Cursor = IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    private void UpdateSelectedText()
    {
        var hasSelection = SelectedItem is not null;

        if (_selectedText is not null)
        {
            _selectedText.Text = GetItemDisplayText(SelectedItem);
            _selectedText.IsVisible = hasSelection;
        }

        if (_placeholderText is not null)
        {
            _placeholderText.IsVisible = !hasSelection;
        }
    }

    private void UpdateListItemTemplate()
    {
        if (_itemsList is not null)
        {
            _itemsList.ItemTemplate = ItemTemplate ?? DefaultItemTemplate;
        }
    }
}
