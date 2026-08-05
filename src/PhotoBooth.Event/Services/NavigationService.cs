using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using PhotoBooth.Event.ViewModels;

namespace PhotoBooth.Event.Services;

/// <summary>
/// Manages navigation between views in the application.
/// Provides factory-based ViewModel creation and lifecycle management (dispose on navigate away).
/// </summary>
/// <remarks>
/// Thread-safety contract: All registrations via <see cref="RegisterViewModel{T}"/>
/// must complete before any <see cref="NavigateTo{T}"/> calls.
/// In practice, registration happens in MainWindowViewModel constructor (E0-S5)
/// before the UI thread starts processing navigation commands.
/// </remarks>
public partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    private readonly Dictionary<Type, Func<ViewModelBase>> _viewModelFactories = new();

    /// <summary>
    /// Registers a factory function for creating instances of <typeparamref name="T"/>.
    /// Must be called during initialization before any navigation occurs.
    /// </summary>
    /// <typeparam name="T">The ViewModel type to register. Must extend <see cref="ViewModelBase"/>.</typeparam>
    /// <param name="factory">A factory function that creates a new instance of <typeparamref name="T"/>.</param>
    public void RegisterViewModel<T>(Func<T> factory) where T : ViewModelBase
    {
        _viewModelFactories[typeof(T)] = () => factory();
    }

    /// <summary>
    /// Navigates to a new instance of <typeparamref name="T"/> created via its registered factory.
    /// Disposes the current view if it implements <see cref="IDisposable"/> before creating the new one.
    /// </summary>
    /// <typeparam name="T">The ViewModel type to navigate to. Must be registered via <see cref="RegisterViewModel{T}"/>.</typeparam>
    public void NavigateTo<T>() where T : ViewModelBase
    {
        if (_viewModelFactories.TryGetValue(typeof(T), out var factory))
        {
            var oldView = CurrentView;

            // Dispose old view FIRST to free memory before new constructor allocates
            DisposeOldView(oldView);

            // Now create and assign — old Bitmaps already freed
            CurrentView = factory();
        }
        else
        {
            Console.WriteLine($"WARNING: No factory registered for {typeof(T).Name}. Navigation skipped.");
        }
    }

    /// <summary>
    /// Navigates to a pre-created ViewModel instance.
    /// Disposes the current view if it implements <see cref="IDisposable"/>.
    /// </summary>
    /// <param name="viewModel">The ViewModel instance to navigate to.</param>
    public void NavigateTo(ViewModelBase viewModel)
    {
        var oldView = CurrentView;

        // Dispose old view FIRST to free memory
        DisposeOldView(oldView);

        // Now assign — old Bitmaps already freed
        CurrentView = viewModel;
    }

    /// <summary>
    /// Safely disposes the old view if it implements <see cref="IDisposable"/>.
    /// </summary>
    /// <param name="view">The view to dispose, or null.</param>
    private void DisposeOldView(ViewModelBase? view)
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
                // Broad catch is intentional: Dispose() may release native resources
                // (camera handles, bitmaps) that can throw unpredictably.
                // We log and continue to avoid crashing on cleanup failures.
                Console.WriteLine($"Error disposing {view.GetType().Name}: {ex.Message}");
            }
        }
    }
}
