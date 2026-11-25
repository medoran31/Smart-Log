using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services;

/// <summary>
/// Seul point d'entrée de l'initialisation de démonstration. Le seeder
/// (SMARTLOG.Data) ne fournit que les données brutes ; ce service applique
/// ensuite les règles métier et journalise toutes les opérations via
/// IJournalService, conformément aux règles 1 et 2 du projet.
/// </summary>
public class DonneesDemonstrationService : IDonneesDemonstrationService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IAvenantService _avenants;
    private readonly ISituationService _situations;
    private readonly IAlerteService _alertes;

    public DonneesDemonstrationService(
        SmartlogDbContext db,
        IJournalService journal,
        IAvenantService avenants,
        ISituationService situations,
        IAlerteService alertes)
    {
        _db = db;
        _journal = journal;
        _avenants = avenants;
        _situations = situations;
        _alertes = alertes;
    }

    public async Task InitialiserAsync(CancellationToken ct = default)
    {
        var resultat = await DonneesDemonstration.InitialiserAsync(_db, ct);
        if (!resultat.DonneesCreees)
            return;

        var utilisateurId = resultat.UtilisateurId;

        // Traçabilité des créations brutes, avant tout recalcul.
        foreach (var role in await _db.Roles.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Role), role.Id,
                "Creation", null, new { role.Id, role.Nom }, ct);
        }

        foreach (var utilisateur in await _db.Utilisateurs.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Utilisateur), utilisateur.Id,
                "Creation", null, new { utilisateur.Id, utilisateur.Identifiant }, ct);
        }

        foreach (var intervenant in await _db.Intervenants.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Intervenant), intervenant.Id,
                "Creation", null,
                new { intervenant.Id, intervenant.RaisonSociale, intervenant.Type }, ct);
        }

        foreach (var projet in await _db.Projets.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Projet), projet.Id,
                "Creation", null,
                new
                {
                    projet.Id,
                    projet.Code,
                    projet.Intitule,
                    projet.MontantInitial,
                    projet.DelaiContractuelJours
                }, ct);
        }

        foreach (var marche in await _db.Marches.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Marche), marche.Id,
                "Creation", null,
                new { marche.Id, marche.ReferenceMarche, marche.ProjetId, marche.DateSignature }, ct);
        }

        foreach (var prix in await _db.PrixUnitaires.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(PrixUnitaire), prix.Id,
                "Creation", null,
                new
                {
                    prix.Id,
                    prix.Reference,
                    prix.Designation,
                    prix.Unite,
                    prix.PrixUnitaireHT,
                    prix.QuantiteContractuelle
                }, ct);
        }

        foreach (var avenant in await _db.Avenants.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Avenant), avenant.Id,
                "Creation", null,
                new
                {
                    avenant.Id,
                    avenant.Numero,
                    avenant.Type,
                    avenant.MontantDelta,
                    avenant.DelaiDeltaJours,
                    avenant.Statut
                }, ct);
        }

        foreach (var situation in await _db.Situations.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Situation), situation.Id,
                "Creation", null,
                new { situation.Id, situation.Periode, situation.Type, situation.Statut }, ct);
        }

        foreach (var reserve in await _db.Reserves.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Reserve), reserve.Id,
                "Creation", null,
                new
                {
                    reserve.Id,
                    reserve.Numero,
                    reserve.Description,
                    reserve.Priorite,
                    reserve.DelaiLeveeJours,
                    reserve.Statut
                }, ct);
        }

        foreach (var courrier in await _db.Courriers.AsNoTracking().ToListAsync(ct))
        {
            await _journal.EnregistrerAsync(utilisateurId, nameof(Courrier), courrier.Id,
                "Creation", null,
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
                }, ct);
        }

        // Valeurs dérivées : uniquement via les services dédiés.
        for (var i = 0; i < resultat.ProjetIds.Count; i++)
        {
            await _avenants.InitialiserProjetAsync(resultat.ProjetIds[i], utilisateurId, ct);
        }

        foreach (var avenantId in resultat.AvenantIds)
        {
            await _avenants.ValiderAsync(avenantId, utilisateurId, ct);
        }

        for (var i = 0; i < resultat.SituationIds.Count; i++)
        {
            var tauxAvancement = i == 0 ? 0.35m : 0.60m;
            await _situations.InitialiserAsync(
                resultat.SituationIds[i], tauxAvancement, 0.05m, utilisateurId, ct: ct);
        }

        // Premier passage du moteur de règles : alimente la cloche de
        // notifications dès l'initialisation de la démonstration.
        await _alertes.AnalyserAsync(utilisateurId: utilisateurId, ct: ct);
    }
}
