using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace RelicLauncher.App.Views.Controls;

public partial class EmptyStatePanel : UserControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<EmptyStatePanel, bool>(nameof(IsActive));

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<EmptyStatePanel, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyStatePanel, string?>(nameof(ActionText));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyStatePanel, ICommand?>(nameof(ActionCommand));

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public EmptyStatePanel()
    {
        InitializeComponent();
    }
}
