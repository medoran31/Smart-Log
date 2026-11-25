using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Smartlog.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T champ, T valeur, [CallerMemberName] string? nom = null)
    {
        if (EqualityComparer<T>.Default.Equals(champ, valeur)) return false;
        champ = valeur;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
        return true;
    }

    protected void Notifier(params string[] noms)
    {
        foreach (var nom in noms)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nom));
    }
}
