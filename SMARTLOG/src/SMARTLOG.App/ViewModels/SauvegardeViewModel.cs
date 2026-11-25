using System.Collections.ObjectModel;
using System.Windows.Input;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Module « Paramètres » : sauvegarde/restauration de la base SQLite et des
/// documents GED. Aucune donnée métier n'est modifiée ici ; une restauration
/// est appliquée au prochain démarrage, jamais sur une base ouverte.
/// </summary>
public class SauvegardeViewModel : ViewModelBase
{
    private readonly ISauvegardeService _sauvegarde;

    public ObservableCollection<SauvegardeResume> Sauvegardes { get; } = new();

    public ICommand CreerCommand { get; }
    public ICommand RestaurerCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand ActualiserCommand { get; }

    private SauvegardeResume? _sauvegardeSelectionnee;
    public SauvegardeResume? SauvegardeSelectionnee
    {
        get => _sauvegardeSelectionnee;
        set
        {
            if (SetField(ref _sauvegardeSelectionnee, value))
            {
                ((RelayCommand)RestaurerCommand).Actualiser();
                ((RelayCommand)SupprimerCommand).Actualiser();
            }
        }
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    private string _message = "Sélectionnez une sauvegarde pour la restaurer.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public string CheminBase => _sauvegarde.CheminBase;
    public string DossierSauvegardes => _sauvegarde.DossierSauvegardes;

    public SauvegardeViewModel(ISauvegardeService sauvegarde)
    {
        _sauvegarde = sauvegarde;

        CreerCommand = new RelayCommand(_ => Creer());
        RestaurerCommand = new RelayCommand(
            _ => Restaurer(), _ => SauvegardeSelectionnee is not null);
        SupprimerCommand = new RelayCommand(
            _ => Supprimer(), _ => SauvegardeSelectionnee is not null);
        ActualiserCommand = new RelayCommand(_ => Charger());

        Charger();
    }

    public void Charger()
    {
        try
        {
            Sauvegardes.Clear();
            foreach (var sauvegarde in _sauvegarde.ListerAsync().GetAwaiter().GetResult())
                Sauvegardes.Add(sauvegarde);
            SauvegardeSelectionnee = Sauvegardes.FirstOrDefault();
            Message = Sauvegardes.Count == 0
                ? "Aucune sauvegarde. Cliquez sur « Créer une sauvegarde »."
                : $"{Sauvegardes.Count} sauvegarde(s) disponible(s).";
        }
        catch (Exception ex)
        {
            Message = $"Lecture des sauvegardes impossible : {ex.Message}";
        }
    }

    private void Creer()
    {
        try
        {
            var resultat = _sauvegarde.CreerAsync(
                string.IsNullOrWhiteSpace(Description) ? null : Description.Trim())
                .GetAwaiter().GetResult();

            Message = resultat.Message;
            if (resultat.Succes)
            {
                Description = string.Empty;
                Charger();
                if (resultat.Sauvegarde is not null)
                    SauvegardeSelectionnee = Sauvegardes.FirstOrDefault(
                        s => s.Chemin == resultat.Sauvegarde.Chemin);
            }
        }
        catch (Exception ex)
        {
            Message = $"Sauvegarde impossible : {ex.Message}";
        }
    }

    private void Restaurer()
    {
        if (SauvegardeSelectionnee is null)
            return;

        try
        {
            var resultat = _sauvegarde.PreparerRestaurationAsync(
                SauvegardeSelectionnee.Chemin).GetAwaiter().GetResult();
            Message = resultat.Succes
                ? resultat.Message + " L'ancienne base sera conservée avant remplacement."
                : resultat.Message;
        }
        catch (Exception ex)
        {
            Message = $"Restauration impossible : {ex.Message}";
        }
    }

    private void Supprimer()
    {
        if (SauvegardeSelectionnee is null)
            return;

        try
        {
            var supprime = _sauvegarde.SupprimerAsync(
                SauvegardeSelectionnee.Chemin).GetAwaiter().GetResult();
            Message = supprime ? "Sauvegarde supprimée." : "Suppression impossible.";
            Charger();
        }
        catch (Exception ex)
        {
            Message = $"Suppression impossible : {ex.Message}";
        }
    }
}
