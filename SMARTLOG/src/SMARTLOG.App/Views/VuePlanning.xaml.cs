using System.Windows.Controls;

namespace Smartlog.App.Views;

/// <summary>
/// Vue de planification : arborescence WBS et Gantt natif synchronisés.
/// Toutes les dates et le chemin critique proviennent d'IPlanificationService.
/// </summary>
public partial class VuePlanning : UserControl
{
    public VuePlanning()
    {
        InitializeComponent();
    }
}
