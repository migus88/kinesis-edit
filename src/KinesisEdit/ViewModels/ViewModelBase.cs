using CommunityToolkit.Mvvm.ComponentModel;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Base class of every view model: property-change notification from
    /// CommunityToolkit.Mvvm. View models stay free of Avalonia types — colors, fonts, and
    /// layout are mapped in XAML from the enums and strings exposed here — so the whole shell
    /// is unit-testable without a UI toolkit.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
    }
}
