namespace RelicLauncher.App.Services;

public interface IStoragePickerService
{
    Task<string?> PickFolderAsync(string? title = null);

    Task<string?> PickImageFileAsync(string? title = null);

    Task<string?> PickZipFileAsync(string? title = null);

    Task<string?> SaveZipFileAsync(string? suggestedFileName = null, string? title = null);

    Task<string?> PickModpackFileAsync(string? title = null);

    Task<string?> SaveModpackFileAsync(string? suggestedFileName = null, string? title = null);
}
