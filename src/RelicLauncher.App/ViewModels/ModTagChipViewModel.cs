using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelicLauncher.Core.Models;

namespace RelicLauncher.App.ViewModels;

public sealed partial class ModTagChipViewModel : ViewModelBase
{
    private readonly Action<ModTagChipViewModel> _toggle;

    public ModTagChipViewModel(ModTagInfo tag, bool isSelected, Action<ModTagChipViewModel> toggle)
    {
        Tag = tag;
        TagId = tag.TagId;
        Name = tag.Name;
        IsSelected = isSelected;
        _toggle = toggle;
    }

    public ModTagInfo Tag { get; }
    public string TagId { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Toggle() => _toggle(this);
}
