using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoBooth.Event.ViewModels;

/// <summary>
/// Abstract base class for all ViewModels in the Event app.
/// Provides INotifyPropertyChanged via CommunityToolkit.Mvvm's ObservableObject.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject
{
}
