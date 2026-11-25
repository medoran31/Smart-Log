using System.Windows.Controls;

namespace Smartlog.App.Views;

/// <summary>
/// Écran de saisie des attachements. Les calculs (montant par ligne,
/// reliquat, alerte de dépassement et cumul des situations) sont entièrement
/// délégués aux services ; la vue ne fait qu'afficher leurs résultats.
/// </summary>
public partial class VueAttachements : UserControl
{
    public VueAttachements()
    {
        InitializeComponent();
    }
}
