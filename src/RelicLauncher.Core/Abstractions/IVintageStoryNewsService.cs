using RelicLauncher.Core.Models;
using RelicLauncher.Core.Results;

namespace RelicLauncher.Core.Abstractions;

public interface IVintageStoryNewsService
{
    Task<Result<IReadOnlyList<NewsArticle>>> FetchLatestAsync(int maxItems, CancellationToken cancellationToken = default);

    Task<Result<NewsArticleDetail>> FetchArticleAsync(string url, CancellationToken cancellationToken = default);
}
