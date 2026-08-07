namespace RelicLauncher.App.Services;

public interface IToastService
{
    Guid Show(ToastRequest request);
    void UpdateProgress(Guid id, string? progressText);
    void Dismiss(Guid id);
    void DismissAll();
}
