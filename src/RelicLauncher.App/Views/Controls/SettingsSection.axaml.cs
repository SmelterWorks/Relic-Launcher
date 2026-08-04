using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;

namespace RelicLauncher.App.Views.Controls;

public partial class SettingsSection : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsSection, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SettingsSection, string?>(nameof(Subtitle));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SettingsSection, bool>(nameof(IsExpanded), defaultValue: true);

    public static readonly StyledProperty<object?> SectionContentProperty =
        AvaloniaProperty.Register<SettingsSection, object?>(nameof(SectionContent));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public object? SectionContent
    {
        get => GetValue(SectionContentProperty);
        set => SetValue(SectionContentProperty, value);
    }

    public IRelayCommand ToggleExpandedCommand { get; }

    public SettingsSection()
    {
        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        InitializeComponent();
    }
}
