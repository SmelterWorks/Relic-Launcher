namespace RelicLauncher.App.Services;

public interface IConfirmDialogService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel");
}
