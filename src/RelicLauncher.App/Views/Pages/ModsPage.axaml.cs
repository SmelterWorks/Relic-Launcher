using Avalonia.Controls;
using Avalonia.Input;
using RelicLauncher.App.ViewModels;

namespace RelicLauncher.App.Views.Pages;

public partial class ModsPage : UserControl
{
    private const double StackBreakpoint = 700;
    private Grid? _browseDetailGrid;
    private bool _stacked;

    public ModsPage()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            _browseDetailGrid = this.FindControl<Grid>("BrowseDetailGrid");
            UpdateLayoutMode(Bounds.Width);
        };
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        => UpdateLayoutMode(e.NewSize.Width);

    private void UpdateLayoutMode(double width)
    {
        if (_browseDetailGrid is null)
        {
            return;
        }

        var stack = width < StackBreakpoint;
        if (stack == _stacked)
        {
            return;
        }

        _stacked = stack;
        if (stack)
        {
            _browseDetailGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _browseDetailGrid.RowDefinitions = new RowDefinitions("*,*");
            if (_browseDetailGrid.Children.Count >= 2)
            {
                Grid.SetColumn(_browseDetailGrid.Children[0], 0);
                Grid.SetRow(_browseDetailGrid.Children[0], 0);
                Grid.SetColumn(_browseDetailGrid.Children[1], 0);
                Grid.SetRow(_browseDetailGrid.Children[1], 1);
            }
        }
        else
        {
            _browseDetailGrid.ColumnDefinitions = new ColumnDefinitions("*,*");
            _browseDetailGrid.RowDefinitions = new RowDefinitions("*");
            if (_browseDetailGrid.Children.Count >= 2)
            {
                Grid.SetColumn(_browseDetailGrid.Children[0], 0);
                Grid.SetRow(_browseDetailGrid.Children[0], 0);
                Grid.SetColumn(_browseDetailGrid.Children[1], 1);
                Grid.SetRow(_browseDetailGrid.Children[1], 0);
            }
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ModsViewModel vm)
        {
            return;
        }

        if (vm.SearchCommand.CanExecute(null))
        {
            vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnBrowseContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is ModRowViewModel row)
        {
            _ = row.LoadLogoAsync();
        }
    }

    private void OnInstalledContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is InstalledModRowViewModel row)
        {
            _ = row.LoadLogoAsync();
        }
    }
}
