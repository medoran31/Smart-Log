using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Smartlog.App.ViewModels;

namespace Smartlog.App.Views;

public partial class VueMarches : UserControl
{
    public VueMarches()
    {
        InitializeComponent();
    }

    private void ImporterPiece_Click(object sender, RoutedEventArgs e)
    {
        var dialogue = new OpenFileDialog
        {
            Title = "Importer une pièce du marché",
            Filter = "Documents (*.pdf;*.doc;*.docx;*.xls;*.xlsx)|*.pdf;*.doc;*.docx;*.xls;*.xlsx"
        };

        if (dialogue.ShowDialog() == true && DataContext is MarchesViewModel viewModel)
            viewModel.ImporterDocument(dialogue.FileName);
    }

    private void Documents_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ListBox liste && liste.SelectedItem is not null && DataContext is MarchesViewModel viewModel)
            viewModel.OuvrirDocumentCommand.Execute(liste.SelectedItem);
    }
}
