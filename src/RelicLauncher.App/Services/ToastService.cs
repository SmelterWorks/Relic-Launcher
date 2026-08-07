using Avalonia.Threading;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Services;

public sealed class ToastService : IToastService
{
    private const int MaxVisible = 3;
    private readonly ToastHostViewModel _host;
    private readonly Dictionary<Guid, CancellationTokenSource> _autoDismiss = new();

    public ToastService(ToastHostViewModel host)
    {
        _host = host;
    }

    public Guid Show(ToastRequest request)
    {
        var id = Guid.NewGuid();
        RunOnUi(() =>
        {
            while (_host.Items.Count >= MaxVisible)
            {
                var oldest = _host.Items[0];
                DismissInternal(oldest.Id);
            }

            void dismiss() => Dismiss(id);

            var actions = (request.Actions ?? [])
                .Select(a => new ToastActionItemViewModel(a.Label, a.Handler, a.DismissOnClick, dismiss))
                .ToList();

            var item = new ToastItemViewModel
            {
                Id = id,
                Title = request.Title,
                Message = request.Message,
                Severity = request.Severity,
                ProgressText = request.ProgressText,
                Actions = actions,
            };

            _host.Items.Add(item);

            var duration = request.Duration ?? GetDefaultDuration(request.Severity);
            if (duration is { } span && span > TimeSpan.Zero)
            {
                var cts = new CancellationTokenSource();
                _autoDismiss[id] = cts;
                _ = AutoDismissAsync(id, span, cts.Token);
            }
        });

        return id;
    }

    public void UpdateProgress(Guid id, string? progressText)
    {
        RunOnUi(() =>
        {
            var item = _host.Items.FirstOrDefault(i => i.Id == id);
            if (item is not null)
            {
                item.ProgressText = progressText;
            }
        });
    }

    public void Dismiss(Guid id)
    {
        RunOnUi(() => DismissInternal(id));
    }

    public void DismissAll()
    {
        RunOnUi(() =>
        {
            foreach (var id in _host.Items.Select(i => i.Id).ToList())
            {
                DismissInternal(id);
            }
        });
    }

    private void DismissInternal(Guid id)
    {
        if (_autoDismiss.Remove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        var item = _host.Items.FirstOrDefault(i => i.Id == id);
        if (item is not null)
        {
            _host.Items.Remove(item);
        }
    }

    private static TimeSpan? GetDefaultDuration(ToastSeverity severity)
    {
        return severity switch
        {
            ToastSeverity.Info => TimeSpan.FromSeconds(5),
            ToastSeverity.Success => TimeSpan.FromSeconds(4),
            _ => null,
        };
    }

    private async Task AutoDismissAsync(Guid id, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
            {
                Dismiss(id);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
