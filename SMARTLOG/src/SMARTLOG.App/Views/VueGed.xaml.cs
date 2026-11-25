using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Smartlog.App.ViewModels;

namespace Smartlog.App.Views;

public partial class VueGed : UserControl
{
    public VueGed()
    {
        InitializeComponent();
    }

    private void Importer_Click(object sender, RoutedEventArgs e)
    {
        var dialogue = new OpenFileDialog
        {
            Title = "Importer des pièces dans la GED",
            Filter = "Documents (*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff)|" +
                     "*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.csv;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff",
            Multiselect = true
        };

        if (dialogue.ShowDialog() == true && DataContext is GedViewModel viewModel)
            viewModel.ImporterFichiers(dialogue.FileNames);
    }

    private void Arborescence_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // TreeView.SelectedItem est en lecture seule en WPF : la sélection est
        // propagée au ViewModel via cet événement.
        if (DataContext is GedViewModel viewModel)
            viewModel.SelectionArborescence = e.NewValue as NoeudArborescence;
    }

    private void Documents_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grille && grille.SelectedItem is not null && DataContext is GedViewModel viewModel)
            viewModel.OuvrirCommand.Execute(grille.SelectedItem);
    }
}
