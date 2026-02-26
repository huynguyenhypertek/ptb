using Avalonia.Controls;
using Avalonia.Input;
using PhotoBooth.Admin.ViewModels;

namespace PhotoBooth.Admin.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            vm.LoginCommand.Execute(null);
        }
    }
}
