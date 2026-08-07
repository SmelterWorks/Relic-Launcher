namespace RelicLauncher.App.ViewModels;

public partial class ModsViewModel
{
    private bool _detailStatusIsError;
    private bool _updateStatusIsError;

    public bool DetailStatusIsError
    {
        get => _detailStatusIsError;
        private set => SetProperty(ref _detailStatusIsError, value);
    }

    public bool UpdateStatusIsError
    {
        get => _updateStatusIsError;
        private set => SetProperty(ref _updateStatusIsError, value);
    }

    private void SetDetailStatus(string message, bool isError = false)
    {
        DetailStatus = message;
        DetailStatusIsError = isError;
    }

    private void SetUpdateStatus(string message, bool isError = false)
    {
        UpdateStatusMessage = message;
        UpdateStatusIsError = isError;
    }
}
