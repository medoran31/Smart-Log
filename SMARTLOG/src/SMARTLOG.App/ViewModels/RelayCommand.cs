using System.Windows.Input;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Commande légère utilisée par les ViewModels pour les actions de l'UI.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _executer;
    private readonly Func<object?, bool>? _peutExecuter;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action<object?> executer, Func<object?, bool>? peutExecuter = null)
    {
        _executer = executer;
        _peutExecuter = peutExecuter;
    }

    public bool CanExecute(object? parametre) => _peutExecuter?.Invoke(parametre) ?? true;

    public void Execute(object? parametre) => _executer(parametre);

    public void Actualiser() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
