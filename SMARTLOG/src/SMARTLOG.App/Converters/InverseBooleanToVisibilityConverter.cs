using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Smartlog.App.Converters;

/// <summary>
/// Inverse du convertisseur booléen de visibilité : <c>true</c> masque l'élément,
/// <c>false</c> l'affiche. Utilisé pour les états vides du tableau de bord.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool booleen && booleen ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility visibilite && visibilite == Visibility.Visible ? false : true;
}
