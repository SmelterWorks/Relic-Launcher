using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace RelicLauncher.App.Services;

public sealed class AvaloniaStoragePickerService(MainWindowHolder windowHolder) : IStoragePickerService
{
    public async Task<string?> PickFolderAsync(string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title ?? "Select folder",
            AllowMultiple = false,
        }).ConfigureAwait(true);

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickImageFileAsync(string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Select image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"],
                },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickZipFileAsync(string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Select mod zip",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Mod zip")
                {
                    Patterns = ["*.zip"],
                },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveZipFileAsync(string? suggestedFileName = null, string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Save backup",
            SuggestedFileName = suggestedFileName ?? "relic-backup.zip",
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType("Zip archive")
                {
                    Patterns = ["*.zip"],
                },
            ],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickModpackFileAsync(string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Select modpack",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Relic modpack")
                {
                    Patterns = ["*.relicmodpack", "*.zip"],
                },
            ],
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveModpackFileAsync(string? suggestedFileName = null, string? title = null)
    {
        var storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Export modpack",
            SuggestedFileName = suggestedFileName ?? "modpack.relicmodpack",
            DefaultExtension = "relicmodpack",
            FileTypeChoices =
            [
                new FilePickerFileType("Relic modpack")
                {
                    Patterns = ["*.relicmodpack"],
                },
            ],
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    private IStorageProvider? GetStorageProvider()
        => windowHolder.Window?.StorageProvider;
}
