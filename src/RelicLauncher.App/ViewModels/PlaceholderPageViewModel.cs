namespace RelicLauncher.App.ViewModels;

public sealed class PlaceholderPageViewModel : PageViewModelBase
{
    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public void Configure(string title, string message)
    {
        Title = title;
        Message = message;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Message));
    }
}
