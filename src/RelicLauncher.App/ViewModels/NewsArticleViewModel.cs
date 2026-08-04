using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class NewsArticleViewModel : ViewModelBase
{
    private readonly Func<NewsArticleViewModel, Task> _onSelect;

    public NewsArticleViewModel(NewsArticle article, Func<NewsArticleViewModel, Task> onSelect)
    {
        _onSelect = onSelect;
        Title = article.Title;
        Url = article.Url;
        PublishedLabel = article.PublishedLabel ?? string.Empty;
    }

    public string Title { get; }
    public string Url { get; }
    public string PublishedLabel { get; }

    [RelayCommand]
    private Task OpenAsync() => _onSelect(this);
}
