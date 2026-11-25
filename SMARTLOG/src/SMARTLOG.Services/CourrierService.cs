using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Service du registre courrier : création, réponse, sans suite et calcul
/// des échéances de réponse. Toutes les écritures sont journalisées.
/// </summary>
public class CourrierService : ICourrierService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;

    public CourrierService(SmartlogDbContext db, IJournalService journal)
    {
        _db = db;
        _journal = journal;
    }

    public async Task<CourrierResume> CreerAsync(
        Guid projetId,
        SensCourrier sens,
        string expediteur,
        string destinataire,
        string objet,
        DateOnly dateCourrier,
        int? delaiReponseJours,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        ValiderSaisie(expediteur, destinataire, objet, dateCourrier, delaiReponseJours);

        if (!await _db.Projets.AnyAsync(p => p.Id == projetId, ct))
            throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var courrier = new Courrier
        {
            ProjetId = projetId,
            Sens = sens,
            Expediteur = expediteur.Trim(),
            Destinataire = destinataire.Trim(),
            Objet = objet.Trim(),
            DateCourrier = dateCourrier,
            DelaiReponseJours = delaiReponseJours,
            Statut = StatutCourrier.EnAttente
        };
        _db.Courriers.Add(courrier);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Courrier), courrier.Id, "Creation", null,
            new
            {
                courrier.Id,
                courrier.ProjetId,
                courrier.Sens,
                courrier.Expediteur,
                courrier.Destinataire,
                courrier.Objet,
                courrier.DateCourrier,
                courrier.DelaiReponseJours
            },
            ct);

        return VersResume(courrier);
    }

    public async Task<CourrierResume> EnregistrerReponseAsync(
        Guid courrierId, DateOnly dateReponse, Guid utilisateurId,
        CancellationToken ct = default)
    {
        var courrier = await _db.Courriers.FirstOrDefaultAsync(c => c.Id == courrierId, ct)
            ?? throw new InvalidOperationException($"Courrier {courrierId} introuvable.");
        if (dateReponse < courrier.DateCourrier)
            throw new ArgumentException(
                "La date de réponse ne peut pas précéder la date du courrier.", nameof(dateReponse));

        var avant = new { courrier.Statut, courrier.DateReponse };
        courrier.Statut = StatutCourrier.Repondu;
        courrier.DateReponse = dateReponse;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Courrier), courrier.Id, "EnregistrementReponse", avant,
            new { courrier.Statut, courrier.DateReponse }, ct);

        return VersResume(courrier);
    }

    public async Task<CourrierResume> MarquerSansSuiteAsync(
        Guid courrierId, Guid utilisateurId, CancellationToken ct = default)
    {
        var courrier = await _db.Courriers.FirstOrDefaultAsync(c => c.Id == courrierId, ct)
            ?? throw new InvalidOperationException($"Courrier {courrierId} introuvable.");
        if (courrier.Statut == StatutCourrier.SansSuite)
            return VersResume(courrier);

        var avant = new { courrier.Statut, courrier.DateReponse };
        courrier.Statut = StatutCourrier.SansSuite;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Courrier), courrier.Id, "SansSuite", avant,
            new { courrier.Statut }, ct);

        return VersResume(courrier);
    }

    public async Task<CourrierResume?> ObtenirAsync(Guid courrierId, CancellationToken ct = default)
    {
        var courrier = await _db.Courriers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == courrierId, ct);
        return courrier is null ? null : VersResume(courrier);
    }

    public async Task<IReadOnlyList<CourrierResume>> ListerAsync(
        Guid? projetId = null,
        SensCourrier? sens = null,
        bool enAttenteSeulement = false,
        CancellationToken ct = default)
    {
        var requete = _db.Courriers.AsNoTracking().AsQueryable();
        if (projetId.HasValue)
            requete = requete.Where(c => c.ProjetId == projetId.Value);
        if (sens.HasValue)
            requete = requete.Where(c => c.Sens == sens.Value);
        if (enAttenteSeulement)
            requete = requete.Where(c => c.Statut == StatutCourrier.EnAttente);

        var courriers = await requete
            .OrderByDescending(c => c.DateCourrier)
            .ThenBy(c => c.Objet)
            .ToListAsync(ct);
        return courriers.Select(VersResume).ToList();
    }

    private static CourrierResume VersResume(Courrier courrier)
    {
        DateOnly? echeance = courrier.DelaiReponseJours.HasValue
            ? courrier.DateCourrier.AddDays(courrier.DelaiReponseJours.Value)
            : null;
        var restants = echeance?.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        var enRetard = courrier.Statut == StatutCourrier.EnAttente &&
                       restants.HasValue && restants.Value < 0;

        return new CourrierResume(
            courrier.Id,
            courrier.ProjetId,
            courrier.Sens,
            LibellerSens(courrier.Sens),
            courrier.Expediteur,
            courrier.Destinataire,
            courrier.Objet,
            courrier.DateCourrier,
            courrier.DelaiReponseJours,
            echeance,
            restants,
            enRetard,
            courrier.Statut,
            LibellerStatut(courrier.Statut),
            courrier.DateReponse);
    }

    private static void ValiderSaisie(
        string expediteur, string destinataire, string objet,
        DateOnly dateCourrier, int? delaiReponseJours)
    {
        if (string.IsNullOrWhiteSpace(expediteur))
            throw new ArgumentException("L'expéditeur est obligatoire.", nameof(expediteur));
        if (string.IsNullOrWhiteSpace(destinataire))
            throw new ArgumentException("Le destinataire est obligatoire.", nameof(destinataire));
        if (string.IsNullOrWhiteSpace(objet))
            throw new ArgumentException("L'objet du courrier est obligatoire.", nameof(objet));
        if (dateCourrier == DateOnly.MinValue)
            throw new ArgumentException("La date du courrier est obligatoire.", nameof(dateCourrier));
        if (delaiReponseJours < 0)
            throw new ArgumentOutOfRangeException(
                nameof(delaiReponseJours), "Le délai de réponse ne peut pas être négatif.");
    }

    private static string LibellerSens(SensCourrier sens) => sens switch
    {
        SensCourrier.Entrant => "Entrant",
        SensCourrier.Sortant => "Sortant",
        _ => sens.ToString()
    };

    private static string LibellerStatut(StatutCourrier statut) => statut switch
    {
        StatutCourrier.EnAttente => "En attente",
        StatutCourrier.Repondu => "Répondu",
        StatutCourrier.SansSuite => "Sans suite",
        _ => statut.ToString()
    };
}
