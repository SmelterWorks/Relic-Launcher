using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RelicLauncher.App.Services;

namespace RelicLauncher.App.ViewModels;

public partial class ToastItemViewModel : ViewModelBase
{
    public Guid Id { get; init; }

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private ToastSeverity _severity = ToastSeverity.Info;

    [ObservableProperty]
    private string? _progressText;

    [ObservableProperty]
    private IReadOnlyList<ToastActionItemViewModel> _actions = [];

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool HasProgress => !string.IsNullOrWhiteSpace(ProgressText);
    public bool HasActions => Actions.Count > 0;
    public bool IsInfo => Severity == ToastSeverity.Info;
    public bool IsSuccess => Severity == ToastSeverity.Success;
    public bool IsWarning => Severity == ToastSeverity.Warning;
    public bool IsError => Severity == ToastSeverity.Error;

    partial void OnTitleChanged(string? value) => OnPropertyChanged(nameof(HasTitle));
    partial void OnProgressTextChanged(string? value) => OnPropertyChanged(nameof(HasProgress));
    partial void OnActionsChanged(IReadOnlyList<ToastActionItemViewModel> value) => OnPropertyChanged(nameof(HasActions));
    partial void OnSeverityChanged(ToastSeverity value)
    {
        OnPropertyChanged(nameof(IsInfo));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsError));
    }
}
