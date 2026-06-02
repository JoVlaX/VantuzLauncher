using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Vantuz.Products.MinecraftLauncher.GUI.Avalonia.ViewModels;

namespace Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
