using System.Windows.Controls;

namespace Smartlog.App.Views;

/// <summary>
/// Écran de suivi financier : indicateurs, graphique d'évolution natif et
/// situations. Toutes les valeurs proviennent d'ISituationService.
/// </summary>
public partial class VueFinances : UserControl
{
    public VueFinances()
    {
        InitializeComponent();
    }
}
