using System.Windows;
using Smartlog.App.ViewModels;

namespace Smartlog.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
