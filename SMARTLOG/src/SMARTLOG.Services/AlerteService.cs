using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Moteur de règles : échéances contractuelles, tâches en retard, réserves
/// non levées et courriers sans réponse. Les alertes sont dérivées des
/// données métier : aucun libellé ni date d'échéance n'est saisi ici.
/// </summary>
public class AlerteService : IAlerteService
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;

    public AlerteService(SmartlogDbContext db, IJournalService journal)
    {
        _db = db;
        _journal = journal;
    }

    public async Task<CentreNotifications> AnalyserAsync(
        Guid? projetId = null,
        Guid utilisateurId = default,
        int horizonJours = 30,
        CancellationToken ct = default)
    {
        if (horizonJours < 0)
            throw new ArgumentOutOfRangeException(
                nameof(horizonJours), "L'horizon d'analyse ne peut pas être négatif.");

        var aujourdHui = DateOnly.FromDateTime(DateTime.Now);
        var limite = aujourdHui.AddDays(horizonJours);

        var projets = await _db.Projets.AsNoTracking()
            .Where(p => !projetId.HasValue || p.Id == projetId.Value)
            .ToListAsync(ct);
        var ids = projets.Select(p => p.Id).ToList();

        var taches = await _db.Taches.AsNoTracking()
            .Where(t => ids.Contains(t.ProjetId)).ToListAsync(ct);
        var reserves = await _db.Reserves.AsNoTracking()
            .Where(r => ids.Contains(r.ProjetId)).ToListAsync(ct);
        var courriers = await _db.Courriers.AsNoTracking()
            .Where(c => ids.Contains(c.ProjetId)).ToListAsync(ct);
        var existantes = await _db.Alertes
            .Where(a => ids.Contains(a.ProjetId)).ToListAsync(ct);

        var candidats = new List<CandidatAlerte>();

        // 1. Échéances contractuelles : achèvement prévu (délai actualisé).
        foreach (var projet in projets.Where(p => p.Statut != StatutProjet.Termine))
        {
            var echeance = projet.DatePrevisionnelleAchevement ??
                           (projet.DateCommencement ?? projet.DateNotification)?
                               .AddDays(projet.DelaiActualiseJours);
            if (echeance.HasValue && echeance.Value <= limite)
                candidats.Add(new CandidatAlerte(
                    projet.Id, nameof(Projet), projet.Id, echeance.Value,
                    PrioriteSelonEcheance(echeance.Value, aujourdHui, PrioriteAlerte.Moyenne)));
        }

        // 2. Tâches en retard : fin prévue dépassée, non réalisées.
        foreach (var tache in taches.Where(t =>
                     t.DateFinPrevue < aujourdHui &&
                     !t.DateFinReelle.HasValue &&
                     t.AvancementPct < 100m))
        {
            var retard = aujourdHui.DayNumber - tache.DateFinPrevue.DayNumber;
            candidats.Add(new CandidatAlerte(
                tache.ProjetId, nameof(Tache), tache.Id, tache.DateFinPrevue,
                retard > 7 ? PrioriteAlerte.Haute : PrioriteAlerte.Moyenne));
        }

        // 3. Réserves non levées dont le délai approche ou est dépassé.
        foreach (var reserve in reserves.Where(r =>
                     r.Statut is not (StatutReserve.Levee or StatutReserve.Cloturee)))
        {
            var echeance = reserve.DateConstatation.AddDays(reserve.DelaiLeveeJours);
            if (echeance > limite)
                continue;
            var priorite = reserve.Priorite == PrioriteReserve.Haute || echeance < aujourdHui
                ? PrioriteAlerte.Haute
                : PrioriteAlerte.Moyenne;
            candidats.Add(new CandidatAlerte(
                reserve.ProjetId, nameof(Reserve), reserve.Id, echeance, priorite));
        }

        // 4. Courriers en attente de réponse et hors délai (ou proches).
        foreach (var courrier in courriers)
        {
            if (courrier.Statut != StatutCourrier.EnAttente ||
                courrier.DelaiReponseJours is not int delaiReponse)
                continue;

            var echeance = courrier.DateCourrier.AddDays(delaiReponse);
            if (echeance > limite)
                continue;
            candidats.Add(new CandidatAlerte(
                courrier.ProjetId, nameof(Courrier), courrier.Id, echeance,
                PrioriteSelonEcheance(echeance, aujourdHui, PrioriteAlerte.Moyenne)));
        }

        var parCle = existantes.ToDictionary(
            a => (a.ProjetId, a.EntiteSourceType, a.EntiteSourceId));
        var creees = 0;
        var modifiees = 0;

        foreach (var candidat in candidats)
        {
            var cle = (candidat.ProjetId, candidat.SourceType, candidat.SourceId);
            if (!parCle.TryGetValue(cle, out var alerte))
            {
                alerte = new Alerte
                {
                    ProjetId = candidat.ProjetId,
                    EntiteSourceType = candidat.SourceType,
                    EntiteSourceId = candidat.SourceId,
                    DateEcheance = candidat.Echeance,
                    Priorite = candidat.Priorite,
                    EstLue = false
                };
                _db.Alertes.Add(alerte);
                await _journal.EnregistrerAsync(
                    utilisateurId, nameof(Alerte), alerte.Id, "CreationAlerte", null,
                    new
                    {
                        alerte.ProjetId,
                        alerte.EntiteSourceType,
                        alerte.EntiteSourceId,
                        alerte.DateEcheance,
                        alerte.Priorite
                    },
                    ct);
                creees++;
                continue;
            }

            if (alerte.DateEcheance != candidat.Echeance || alerte.Priorite != candidat.Priorite)
            {
                var avant = new { alerte.DateEcheance, alerte.Priorite, alerte.EstLue };
                alerte.DateEcheance = candidat.Echeance;
                alerte.Priorite = candidat.Priorite;
                alerte.EstLue = false;
                await _journal.EnregistrerAsync(
                    utilisateurId, nameof(Alerte), alerte.Id, "RecalculAlerte", avant,
                    new { alerte.DateEcheance, alerte.Priorite, alerte.EstLue }, ct);
                modifiees++;
            }
        }

        var cibles = candidats.Select(c => (c.ProjetId, c.SourceType, c.SourceId))
            .ToHashSet();
        var supprimees = 0;
        foreach (var obsolete in existantes.Where(a =>
                     !cibles.Contains((a.ProjetId, a.EntiteSourceType, a.EntiteSourceId))))
        {
            await _journal.EnregistrerAsync(
                utilisateurId, nameof(Alerte), obsolete.Id, "SuppressionAlerte",
                new { obsolete.EntiteSourceType, obsolete.EntiteSourceId, obsolete.DateEcheance },
                null, ct);
            _db.Alertes.Remove(obsolete);
            supprimees++;
        }

        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Alerte), Guid.Empty, "AnalyseAlertes", null,
            new
            {
                ProjetId = projetId,
                HorizonJours = horizonJours,
                AlertesCreees = creees,
                AlertesModifiees = modifiees,
                AlertesSupprimees = supprimees
            },
            ct);

        return await ObtenirCentreAsync(projetId, true, ct);
    }

    public async Task<CentreNotifications> ObtenirCentreAsync(
        Guid? projetId = null, bool inclureLues = true, CancellationToken ct = default)
    {
        var requete = _db.Alertes.AsNoTracking().AsQueryable();
        if (projetId.HasValue)
            requete = requete.Where(a => a.ProjetId == projetId.Value);
        if (!inclureLues)
            requete = requete.Where(a => !a.EstLue);

        var alertes = await requete
            .OrderByDescending(a => a.Priorite)
            .ThenBy(a => a.DateEcheance)
            .ToListAsync(ct);
        if (alertes.Count == 0)
            return new CentreNotifications(Array.Empty<AlerteResume>(), 0, 0);

        var ids = alertes.Select(a => a.ProjetId).Distinct().ToList();
        var projets = await _db.Projets.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        var taches = await _db.Taches.AsNoTracking().ToListAsync(ct);
        var reserves = await _db.Reserves.AsNoTracking().ToListAsync(ct);
        var courriers = await _db.Courriers.AsNoTracking().ToListAsync(ct);

        var resumees = alertes
            .Select(a => VersResume(
                a,
                projets.GetValueOrDefault(a.ProjetId),
                taches.FirstOrDefault(t => t.Id == a.EntiteSourceId),
                reserves.FirstOrDefault(r => r.Id == a.EntiteSourceId),
                courriers.FirstOrDefault(c => c.Id == a.EntiteSourceId)))
            .ToList();

        return new CentreNotifications(
            resumees,
            resumees.Count(a => !a.EstLue),
            resumees.Count(a => a.EstEnRetard));
    }

    public async Task MarquerLueAsync(
        Guid alerteId, Guid utilisateurId, CancellationToken ct = default)
    {
        var alerte = await _db.Alertes.FirstOrDefaultAsync(a => a.Id == alerteId, ct)
            ?? throw new InvalidOperationException($"Alerte {alerteId} introuvable.");
        if (alerte.EstLue)
            return;

        alerte.EstLue = true;
        await _db.SaveChangesAsync(ct);
        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Alerte), alerte.Id, "MarquageAlerte",
            new { EstLue = false }, new { EstLue = true }, ct);
    }

    public async Task MarquerToutesLuesAsync(
        Guid? projetId, Guid utilisateurId, CancellationToken ct = default)
    {
        var alertes = await _db.Alertes
            .Where(a => !a.EstLue && (!projetId.HasValue || a.ProjetId == projetId.Value))
            .ToListAsync(ct);
        foreach (var alerte in alertes)
            alerte.EstLue = true;
        if (alertes.Count == 0)
            return;

        await _db.SaveChangesAsync(ct);
        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Alerte), Guid.Empty, "MarquageToutesAlertes",
            null, new { NombreAlertes = alertes.Count }, ct);
    }

    private static AlerteResume VersResume(
        Alerte alerte,
        Projet? projet,
        Tache? tache,
        Reserve? reserve,
        Courrier? courrier)
    {
        var aujourdHui = DateOnly.FromDateTime(DateTime.Now);
        var restants = alerte.DateEcheance.DayNumber - aujourdHui.DayNumber;

        var (titre, message, menuCible, typeLibelle) = alerte.EntiteSourceType switch
        {
            nameof(Projet) when projet is not null => (
                $"Échéance contractuelle — {projet.Code}",
                $"L'achèvement prévu du projet « {projet.Intitule} » est au {FormaterDate(alerte.DateEcheance)}.",
                "Fiche projet",
                "Échéance contractuelle"),
            nameof(Tache) when tache is not null => (
                $"Tâche en retard — {tache.Libelle}",
                $"La tâche « {tache.Libelle} » devait se terminer le {FormaterDate(alerte.DateEcheance)} " +
                $"(avancement : {tache.AvancementPct:0} %).",
                "Planning",
                "Tâche"),
            nameof(Reserve) when reserve is not null => (
                $"Réserve n°{reserve.Numero} non levée",
                reserve.Description,
                "Réserves",
                "Réserve"),
            nameof(Courrier) when courrier is not null => (
                $"Courrier sans réponse — {courrier.Objet}",
                $"{(courrier.Sens == SensCourrier.Entrant ? "Courrier entrant" : "Courrier sortant")} " +
                $"de « {courrier.Expediteur} » à « {courrier.Destinataire} » : " +
                $"réponse attendue pour le {FormaterDate(alerte.DateEcheance)}.",
                "Courrier",
                "Courrier"),
            _ => (
                "Alerte",
                "L'élément à l'origine de cette alerte n'est plus disponible.",
                "Tableau de bord",
                alerte.EntiteSourceType)
        };

        return new AlerteResume(
            alerte.Id,
            alerte.ProjetId,
            projet?.Code ?? "—",
            projet?.Intitule ?? "Projet inconnu",
            alerte.EntiteSourceType,
            typeLibelle,
            alerte.EntiteSourceId,
            titre,
            message,
            menuCible,
            alerte.DateEcheance,
            restants,
            restants < 0,
            alerte.Priorite,
            LibellerPriorite(alerte.Priorite),
            alerte.EstLue);
    }

    private static string FormaterDate(DateOnly date) => date.ToString("d", Fr);

    private static string LibellerPriorite(PrioriteAlerte priorite) => priorite switch
    {
        PrioriteAlerte.Basse => "Basse",
        PrioriteAlerte.Moyenne => "Moyenne",
        PrioriteAlerte.Haute => "Haute",
        _ => priorite.ToString()
    };

    private static PrioriteAlerte PrioriteSelonEcheance(
        DateOnly echeance, DateOnly aujourdHui, PrioriteAlerte parDefaut)
    {
        var jours = echeance.DayNumber - aujourdHui.DayNumber;
        if (jours <= 7)
            return PrioriteAlerte.Haute;
        return parDefaut;
    }

    private sealed record CandidatAlerte(
        Guid ProjetId,
        string SourceType,
        Guid SourceId,
        DateOnly Echeance,
        PrioriteAlerte Priorite);
}
