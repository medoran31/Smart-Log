using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services;

public class JournalService : IJournalService
{
    private readonly SmartlogDbContext _db;

    public JournalService(SmartlogDbContext db) => _db = db;

    public async Task EnregistrerAsync(
        Guid utilisateurId,
        string entiteType,
        Guid entiteId,
        string action,
        object? valeurAvant,
        object? valeurApres,
        CancellationToken ct = default)
    {
        _db.JournalActions.Add(new JournalAction
        {
            UtilisateurId = utilisateurId,
            EntiteType = entiteType,
            EntiteId = entiteId,
            Action = action,
            ValeurAvantJson = valeurAvant is null ? null : JsonSerializer.Serialize(valeurAvant),
            ValeurApresJson = valeurApres is null ? null : JsonSerializer.Serialize(valeurApres),
            DateAction = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EntreeJournal>> ListerAsync(
        string entiteType,
        Guid entiteId,
        CancellationToken ct = default)
    {
        var actions = await _db.JournalActions
            .AsNoTracking()
            .Include(j => j.Utilisateur)
            .Where(j => j.EntiteType == entiteType && j.EntiteId == entiteId)
            .OrderByDescending(j => j.DateAction)
            .ToListAsync(ct);

        return actions.Select(j => new EntreeJournal(
            j.Id,
            j.DateAction,
            TraduireAction(j.Action),
            j.ValeurAvantJson,
            j.ValeurApresJson,
            j.Utilisateur?.Identifiant ?? "Utilisateur inconnu")).ToList();
    }

    private static string TraduireAction(string action) => action switch
    {
        "Creation" => "Création",
        "Modification" => "Modification",
        "ChangementStatut" => "Changement de statut",
        "SuppressionProjet" => "Suppression de projet",
        "InitialisationValeursDerivees" => "Initialisation des valeurs dérivées",
        "RecalculAvenant" => "Recalcul suite à avenant",
        "RecalculValeursDerivees" => "Recalcul des valeurs dérivées",
        "RecalculCroise" => "Recalcul croisé (finances, planning, alertes)",
        "AnnulationAvenant" => "Annulation d'avenant",
        "CalculMontantsDerives" => "Calcul des montants dérivés",
        "AjoutLigne" => "Ajout de ligne",
        "ModificationQuantite" => "Modification de quantité",
        "SuppressionLigne" => "Suppression de ligne",
        "RecalculApresBpu" => "Recalcul du montant de ligne après BPU",
        "RecalculSituation" => "Recalcul de situation",
        "RegroupementAttachements" => "Regroupement des attachements",
        "ModificationPlanning" => "Modification du planning",
        "SuppressionTache" => "Suppression de tâche",
        "AjoutLiaison" => "Ajout de liaison",
        "SuppressionLiaison" => "Suppression de liaison",
        "RecalculPlanning" => "Recalcul du planning",
        "GenerationPvLevee" => "Génération du PV de levée",
        "EnregistrementReponse" => "Enregistrement de la réponse",
        "SansSuite" => "Classement sans suite",
        "AnalyseAlertes" => "Analyse des alertes",
        "CreationAlerte" => "Création d'alerte",
        "RecalculAlerte" => "Recalcul d'alerte",
        "SuppressionAlerte" => "Suppression d'alerte",
        "MarquageAlerte" => "Alerte marquée comme lue",
        "MarquageToutesAlertes" => "Alertes marquées comme lues",
        "ApplicationSuggestion" => "Suggestion IA appliquée",
        "SuggestionIgnoree" => "Suggestion IA ignorée",
        _ => action
    };
}
