using System;
using System.Collections.Generic;
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
            
            // Dispose old view FIRST to free memory before new constructor allocates
            DisposeOldView(oldView);
            
            // Now create and assign — old Bitmaps already freed
            CurrentView = factory();
        }
    }

    public void NavigateTo(ObservableObject viewModel)
    {
        var oldView = CurrentView;
        
        // Dispose old view FIRST to free memory
        DisposeOldView(oldView);
        
        // Now assign — old Bitmaps already freed
        CurrentView = viewModel;
    }

    private void DisposeOldView(ObservableObject? view)
    {
        if (view is IDisposable disposable)
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
        }
    }
}
