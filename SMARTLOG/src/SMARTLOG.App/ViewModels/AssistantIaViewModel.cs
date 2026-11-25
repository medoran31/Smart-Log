using System.Collections.ObjectModel;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>Message affiché dans la conversation, avec son rôle explicite.</summary>
public sealed record MessageAffichage(RoleMessageAssistant Role, string Contenu, DateTime Horodatage)
{
    public string RoleLibelle => Role == RoleMessageAssistant.Assistant ? "Assistant IA" : "Vous";
}

/// <summary>
/// Panneau « Assistant IA » : conversation contextualisée au projet ouvert,
/// provenance séparée et suggestions appliquées uniquement sur validation.
/// </summary>
public class AssistantIaViewModel : ViewModelBase
{
    private readonly IAssistantIaService _assistant;
    private readonly IProjetService _projets;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<MessageAffichage> Conversation { get; } = new();
    public ObservableCollection<ExtraitDocument> Extraits { get; } = new();
    public ObservableCollection<CalculSysteme> Calculs { get; } = new();
    public ObservableCollection<SuggestionAssistant> Suggestions { get; } = new();

    public ICommand EnvoyerCommand { get; }
    public ICommand EffacerCommand { get; }
    public ICommand ValiderAppliquerCommand { get; }
    public ICommand IgnorerCommand { get; }

    /// <summary>Notifie qu'une suggestion a été appliquée et que les autres modules doivent se recharger.</summary>
    public event EventHandler<(string TypeAction, Guid ProjetId)>? SuggestionAppliquee;

    private ProjetResume? _projetSelectionne;
    public ProjetResume? ProjetSelectionne
    {
        get => _projetSelectionne;
        set
        {
            if (SetField(ref _projetSelectionne, value))
            {
                EffacerConversation();
                Message = value is null
                    ? "Sélectionnez un projet pour contextualiser l'assistant."
                    : $"Contexte chargé : {value.Code} — {value.Intitule}.";
                ActualiserCommandes();
            }
        }
    }

    private string _question = string.Empty;
    public string Question
    {
        get => _question;
        set
        {
            if (SetField(ref _question, value))
                ((RelayCommand)EnvoyerCommand).Actualiser();
        }
    }

    private string _message;
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private string _resumeConfiguration = string.Empty;
    public string ResumeConfiguration
    {
        get => _resumeConfiguration;
        private set => SetField(ref _resumeConfiguration, value);
    }

    private string _contexteProjet = "Aucun contexte chargé.";
    public string ContexteProjet
    {
        get => _contexteProjet;
        private set => SetField(ref _contexteProjet, value);
    }

    private SuggestionAssistant? _suggestionSelectionnee;
    public SuggestionAssistant? SuggestionSelectionnee
    {
        get => _suggestionSelectionnee;
        set
        {
            if (SetField(ref _suggestionSelectionnee, value))
                ActualiserCommandes();
        }
    }

    public AssistantIaViewModel(IAssistantIaService assistant, IProjetService projets)
    {
        _assistant = assistant;
        _projets = projets;

        EnvoyerCommand = new RelayCommand(_ => Envoyer(), _ => PeutEnvoyer());
        EffacerCommand = new RelayCommand(_ => EffacerConversation());
        ValiderAppliquerCommand = new RelayCommand(
            _ => ValiderAppliquer(),
            _ => SuggestionSelectionnee is not null &&
                 !SuggestionSelectionnee.EstTraitee &&
                 ProjetSelectionne is not null &&
                 SuggestionSelectionnee.TypeAction != TypeSuggestionAssistant.Aucune);
        IgnorerCommand = new RelayCommand(
            _ => Ignorer(),
            _ => SuggestionSelectionnee is not null && !SuggestionSelectionnee.EstTraitee);

        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);

        ResumeConfiguration = _assistant.ResumeConfiguration;
        _message = _assistant.EstConfigure
            ? "Posez votre question sur le projet ouvert."
            : "Claude n'est pas configuré : voir le message d'aide ci-dessus.";
    }

    /// <summary>Aligne l'assistant sur le projet ouvert dans l'application.</summary>
    public void SelectionnerProjet(Guid projetId)
    {
        var projet = Projets.FirstOrDefault(p => p.Id == projetId);
        if (projet is not null && !ReferenceEquals(projet, ProjetSelectionne))
            ProjetSelectionne = projet;
    }

    public void Recharger()
    {
        Projets.Clear();
        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);

        ResumeConfiguration = _assistant.ResumeConfiguration;
        ActualiserCommandes();
    }

    private bool PeutEnvoyer() =>
        !string.IsNullOrWhiteSpace(Question) &&
        ProjetSelectionne is not null &&
        _assistant.EstConfigure;

    private void Envoyer()
    {
        if (!PeutEnvoyer())
        {
            Message = "Sélectionnez un projet, saisissez une question et configurez la clé Claude.";
            return;
        }

        try
        {
            var historique = Conversation
                .Select(m => new MessageConversation(m.Role, m.Contenu, m.Horodatage))
                .ToList();

            var reponse = _assistant.PoserQuestionAsync(
                ProjetSelectionne!.Id,
                Question,
                historique,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

            var maintenant = DateTime.Now;
            Conversation.Add(new MessageAffichage(RoleMessageAssistant.Utilisateur, Question.Trim(), maintenant));
            Conversation.Add(new MessageAffichage(RoleMessageAssistant.Assistant, reponse.Texte, maintenant.AddSeconds(1)));

            ContexteProjet = reponse.ContexteProjet;

            Extraits.Clear();
            foreach (var extrait in reponse.Extraits)
                Extraits.Add(extrait);

            Calculs.Clear();
            foreach (var calcul in reponse.Calculs)
                Calculs.Add(calcul);

            Suggestions.Clear();
            foreach (var suggestion in reponse.Suggestions)
                Suggestions.Add(suggestion);
            SuggestionSelectionnee = Suggestions.FirstOrDefault();

            Question = string.Empty;
            Message = reponse.Suggestions.Count > 0
                ? $"Réponse reçue : {reponse.Suggestions.Count} suggestion(s) en attente de validation."
                : "Réponse reçue. Aucune suggestion à appliquer.";
        }
        catch (Exception ex)
        {
            Message = $"Question impossible : {ex.Message}";
        }
    }

    private void ValiderAppliquer()
    {
        var suggestion = SuggestionSelectionnee;
        if (suggestion is null || ProjetSelectionne is null || suggestion.EstTraitee)
            return;

        try
        {
            var resultat = _assistant.AppliquerSuggestionAsync(
                suggestion,
                ProjetSelectionne.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

            if (!resultat.Succes)
            {
                Message = resultat.Message;
                return;
            }

            RemplacerSuggestion(suggestion.MarquerAppliquee(resultat.Message));
            Message = resultat.Message;
            SuggestionAppliquee?.Invoke(
                this, (suggestion.TypeAction.ToString(), ProjetSelectionne.Id));
        }
        catch (Exception ex)
        {
            Message = $"Application impossible : {ex.Message}";
        }
    }

    private void Ignorer()
    {
        var suggestion = SuggestionSelectionnee;
        if (suggestion is null || suggestion.EstTraitee)
            return;

        try
        {
            var resultat = _assistant.IgnorerSuggestionAsync(
                suggestion,
                ProjetSelectionne?.Id ?? Guid.Empty,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

            RemplacerSuggestion(suggestion.MarquerIgnoree());
            Message = resultat.Message;
        }
        catch (Exception ex)
        {
            Message = $"Ignor impossible : {ex.Message}";
        }
    }

    private void RemplacerSuggestion(SuggestionAssistant suggestionModifiee)
    {
        var index = Suggestions.ToList().FindIndex(s => s.Id == suggestionModifiee.Id);
        if (index < 0)
            return;

        Suggestions[index] = suggestionModifiee;
        SuggestionSelectionnee = suggestionModifiee;
    }

    private void EffacerConversation()
    {
        Conversation.Clear();
        Extraits.Clear();
        Calculs.Clear();
        Suggestions.Clear();
        SuggestionSelectionnee = null;
        ContexteProjet = "Aucun contexte chargé.";
    }

    private void ActualiserCommandes()
    {
        ((RelayCommand)EnvoyerCommand).Actualiser();
        ((RelayCommand)ValiderAppliquerCommand).Actualiser();
        ((RelayCommand)IgnorerCommand).Actualiser();
    }
}
