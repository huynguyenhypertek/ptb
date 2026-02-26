using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PhotoBooth.UI.Services;

/// <summary>
/// Manages navigation between views in the application.
/// </summary>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private ObservableObject? _currentView;

    private readonly Dictionary<Type, Func<ObservableObject>> _viewModelFactories = new();

    public void RegisterViewModel<T>(Func<T> factory) where T : ObservableObject
    {
        _viewModelFactories[typeof(T)] = () => factory();
    }

    public void NavigateTo<T>() where T : ObservableObject
    {
        if (_viewModelFactories.TryGetValue(typeof(T), out var factory))
        {
            var oldView = CurrentView;
            
            // Create new view first to ensure responsiveness
            CurrentView = factory();
            
            // Dispose old view asynchronously to avoid blocking UI
            DisposeViewAsync(oldView);
        }
    }

    public void NavigateTo(ObservableObject viewModel)
    {
        var oldView = CurrentView;
        CurrentView = viewModel;
        DisposeViewAsync(oldView);
    }

    private async void DisposeViewAsync(ObservableObject? view)
    {
        if (view is IDisposable disposable)
        {
            await Task.Run(() =>
            {
                try
                {
                    disposable.Dispose();
                    Console.WriteLine($"Disposed: {view.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing {view.GetType().Name}: {ex.Message}");
                }
            });
        }
    }
}
