using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Unique point d'écriture et de calcul des attachements. Les montants par
/// ligne et les reliquats sont calculés ici ; l'interface se contente de
/// les afficher (avec une alerte visuelle en cas de dépassement).
/// </summary>
public class AttachementService : IAttachementService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;

    public AttachementService(SmartlogDbContext db, IJournalService journal)
    {
        _db = db;
        _journal = journal;
    }

    public async Task<AttachementDetail> CreerAsync(
        Guid projetId, DateOnly date, Guid utilisateurId, CancellationToken ct = default)
    {
        var existe = await _db.Projets.AnyAsync(p => p.Id == projetId, ct);
        if (!existe)
            throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var attachement = new Attachement
        {
            ProjetId = projetId,
            Date = date,
            Statut = StatutAttachement.Brouillon
        };
        _db.Attachements.Add(attachement);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Attachement), attachement.Id, "Creation", null,
            new { attachement.Id, attachement.ProjetId, attachement.Date, attachement.Statut }, ct);

        return await ObtenirAsync(attachement.Id, ct)
            ?? throw new InvalidOperationException("Attachement introuvable après création.");
    }

    public async Task<LigneAttachementResume> AjouterLigneAsync(
        Guid attachementId,
        Guid prixUnitaireId,
        decimal quantiteExecutee,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (quantiteExecutee < 0)
            throw new ArgumentOutOfRangeException(nameof(quantiteExecutee), "La quantité exécutée ne peut pas être négative.");

        var attachement = await _db.Attachements
            .FirstOrDefaultAsync(a => a.Id == attachementId, ct)
            ?? throw new InvalidOperationException($"Attachement {attachementId} introuvable.");

        var prix = await _db.PrixUnitaires
            .Include(p => p.Marche)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prixUnitaireId, ct)
            ?? throw new InvalidOperationException($"Prix unitaire {prixUnitaireId} introuvable.");

        if (prix.Marche?.ProjetId != attachement.ProjetId)
            throw new ArgumentException("Ce prix unitaire n'appartient pas au marché du projet.", nameof(prixUnitaireId));

        var ligne = new LigneAttachement
        {
            AttachementId = attachementId,
            PrixUnitaireId = prixUnitaireId,
            QuantiteExecutee = quantiteExecutee,
            MontantCalcule = CalculerMontant(quantiteExecutee, prix.PrixUnitaireHT)
        };
        _db.LignesAttachement.Add(ligne);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(LigneAttachement), ligne.Id, "AjoutLigne", null,
            new
            {
                ligne.Id,
                ligne.AttachementId,
                ligne.PrixUnitaireId,
                ligne.QuantiteExecutee,
                PrixUnitaireHT = prix.PrixUnitaireHT,
                ligne.MontantCalcule
            },
            ct);

        return await TrouverLigneAsync(ligne.Id, attachement.ProjetId, ct);
    }

    public async Task<LigneAttachementResume> SimulerLigneAsync(
        Guid projetId,
        Guid prixUnitaireId,
        decimal quantiteExecutee,
        Guid? ligneIdAExclure = null,
        CancellationToken ct = default)
    {
        var prix = await _db.PrixUnitaires
            .Include(p => p.Marche)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prixUnitaireId, ct)
            ?? throw new InvalidOperationException($"Prix unitaire {prixUnitaireId} introuvable.");

        if (prix.Marche?.ProjetId != projetId)
            throw new ArgumentException("Ce prix unitaire n'appartient pas au marché du projet.", nameof(prixUnitaireId));

        var total = await (from ligne in _db.LignesAttachement
                           join attachement in _db.Attachements on ligne.AttachementId equals attachement.Id
                           where attachement.ProjetId == projetId && ligne.PrixUnitaireId == prixUnitaireId
                           select ligne).ToListAsync(ct);
        var dejaExecute = total
            .Where(l => l.Id != ligneIdAExclure)
            .Sum(l => l.QuantiteExecutee);
        var reliquatAvant = prix.QuantiteContractuelle - dejaExecute;

        return new LigneAttachementResume(
            Guid.Empty,
            Guid.Empty,
            prix.Id,
            prix.Reference,
            prix.Designation,
            prix.Unite,
            prix.PrixUnitaireHT,
            prix.QuantiteContractuelle,
            quantiteExecutee,
            dejaExecute,
            reliquatAvant,
            reliquatAvant - quantiteExecutee,
            CalculerMontant(quantiteExecutee, prix.PrixUnitaireHT),
            quantiteExecutee > reliquatAvant,
            "Simulation");
    }

    public async Task<LigneAttachementResume> ModifierQuantiteAsync(
        Guid ligneId,
        decimal quantiteExecutee,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (quantiteExecutee < 0)
            throw new ArgumentOutOfRangeException(nameof(quantiteExecutee), "La quantité exécutée ne peut pas être négative.");

        var ligne = await _db.LignesAttachement
            .Include(l => l.PrixUnitaire)
            .FirstOrDefaultAsync(l => l.Id == ligneId, ct)
            ?? throw new InvalidOperationException($"Ligne d'attachement {ligneId} introuvable.");

        var avant = new { ligne.QuantiteExecutee, ligne.MontantCalcule };

        ligne.QuantiteExecutee = quantiteExecutee;
        ligne.MontantCalcule = CalculerMontant(quantiteExecutee, ligne.PrixUnitaire!.PrixUnitaireHT);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(LigneAttachement), ligne.Id, "ModificationQuantite",
            avant,
            new { ligne.QuantiteExecutee, ligne.MontantCalcule }, ct);

        var attachement = await _db.Attachements
            .AsNoTracking()
            .FirstAsync(a => a.Id == ligne.AttachementId, ct);
        return await TrouverLigneAsync(ligne.Id, attachement.ProjetId, ct);
    }

    public async Task SupprimerLigneAsync(
        Guid ligneId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var ligne = await _db.LignesAttachement
            .Include(l => l.PrixUnitaire)
            .FirstOrDefaultAsync(l => l.Id == ligneId, ct)
            ?? throw new InvalidOperationException($"Ligne d'attachement {ligneId} introuvable.");

        var avant = new
        {
            ligne.Id,
            ligne.AttachementId,
            ligne.PrixUnitaireId,
            ligne.QuantiteExecutee,
            ligne.MontantCalcule
        };

        _db.LignesAttachement.Remove(ligne);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(LigneAttachement), ligneId, "SuppressionLigne", avant, null, ct);
    }

    public async Task<int> RecalculerLignesAsync(
        Guid prixUnitaireId, Guid utilisateurId, CancellationToken ct = default)
    {
        var prix = await _db.PrixUnitaires
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == prixUnitaireId, ct)
            ?? throw new InvalidOperationException($"Prix unitaire {prixUnitaireId} introuvable.");

        var lignes = await _db.LignesAttachement
            .Where(l => l.PrixUnitaireId == prixUnitaireId)
            .ToListAsync(ct);

        foreach (var ligne in lignes)
        {
            var nouveauMontant = CalculerMontant(ligne.QuantiteExecutee, prix.PrixUnitaireHT);
            if (ligne.MontantCalcule == nouveauMontant)
                continue;

            var avant = new { ligne.MontantCalcule };
            ligne.MontantCalcule = nouveauMontant;
            await _journal.EnregistrerAsync(
                utilisateurId, nameof(LigneAttachement), ligne.Id, "RecalculApresBpu",
                avant, new { ligne.MontantCalcule }, ct);
        }

        if (lignes.Count > 0)
            await _db.SaveChangesAsync(ct);

        return lignes.Count;
    }

    public async Task<AttachementDetail> ChangerStatutAsync(
        Guid attachementId,
        StatutAttachement statut,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var attachement = await _db.Attachements
            .FirstOrDefaultAsync(a => a.Id == attachementId, ct)
            ?? throw new InvalidOperationException($"Attachement {attachementId} introuvable.");

        var avant = attachement.Statut;
        attachement.Statut = statut;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Attachement), attachementId, "ChangementStatut",
            new { Statut = avant.ToString() }, new { Statut = statut.ToString() }, ct);

        return await ObtenirAsync(attachementId, ct)
            ?? throw new InvalidOperationException("Attachement introuvable après changement de statut.");
    }

    public async Task<AttachementDetail?> ObtenirAsync(Guid attachementId, CancellationToken ct = default)
    {
        var attachement = await _db.Attachements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachementId, ct);
        if (attachement is null)
            return null;

        var lignes = await ListerLignesAsync(attachement.ProjetId, ct);
        return VersDetail(attachement, lignes.Where(l => l.AttachementId == attachementId).ToList());
    }

    public async Task<IReadOnlyList<AttachementResume>> ListerAsync(Guid projetId, CancellationToken ct = default)
    {
        var attachements = await _db.Attachements
            .AsNoTracking()
            .Where(a => a.ProjetId == projetId)
            .OrderBy(a => a.Date)
            .ToListAsync(ct);
        var lignes = await ListerLignesAsync(projetId, ct);

        return attachements.Select(a =>
        {
            var lignesAttachement = lignes.Where(l => l.AttachementId == a.Id).ToList();
            return new AttachementResume(
                a.Id, a.ProjetId, a.Date, LibelleStatut(a.Statut), a.Statut,
                lignesAttachement.Count, lignesAttachement.Sum(l => l.MontantCalcule));
        }).ToList();
    }

    public async Task<IReadOnlyList<LigneAttachementResume>> ListerLignesAsync(
        Guid projetId, CancellationToken ct = default)
    {
        var donnees = await (from ligne in _db.LignesAttachement
                             join attachement in _db.Attachements on ligne.AttachementId equals attachement.Id
                             where attachement.ProjetId == projetId
                             select new { ligne, attachement.Statut })
            .AsNoTracking()
            .ToListAsync(ct);

        if (donnees.Count == 0)
            return Array.Empty<LigneAttachementResume>();

        var prixIds = donnees.Select(d => d.ligne.PrixUnitaireId).Distinct().ToList();
        var prixParId = await _db.PrixUnitaires
            .AsNoTracking()
            .Where(p => prixIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // SQLite ne sait pas agréger des decimal côté serveur : le total est
        // calculé en mémoire à partir des lignes déjà matérialisées.
        var totauxParPrix = donnees
            .GroupBy(d => d.ligne.PrixUnitaireId)
            .ToDictionary(groupe => groupe.Key, groupe => groupe.Sum(d => d.ligne.QuantiteExecutee));

        return donnees.Select(d =>
        {
            var ligne = d.ligne;
            var prix = prixParId[ligne.PrixUnitaireId];
            var totalExecute = totauxParPrix.TryGetValue(ligne.PrixUnitaireId, out var total) ? total : 0m;
            var ailleurs = totalExecute - ligne.QuantiteExecutee;
            var reliquatAvant = prix.QuantiteContractuelle - ailleurs;

            return new LigneAttachementResume(
                ligne.Id,
                ligne.AttachementId,
                ligne.PrixUnitaireId,
                prix.Reference,
                prix.Designation,
                prix.Unite,
                prix.PrixUnitaireHT,
                prix.QuantiteContractuelle,
                ligne.QuantiteExecutee,
                ailleurs,
                reliquatAvant,
                reliquatAvant - ligne.QuantiteExecutee,
                CalculerMontant(ligne.QuantiteExecutee, prix.PrixUnitaireHT),
                ligne.QuantiteExecutee > reliquatAvant,
                LibelleStatut(d.Statut));
        }).OrderBy(l => l.ReferencePrix).ThenBy(l => l.AttachementId).ToList();
    }

    private async Task<LigneAttachementResume> TrouverLigneAsync(
        Guid ligneId, Guid projetId, CancellationToken ct) =>
        (await ListerLignesAsync(projetId, ct)).Single(l => l.Id == ligneId);

    private static AttachementDetail VersDetail(
        Attachement attachement, IReadOnlyList<LigneAttachementResume> lignes) =>
        new(
            attachement.Id,
            attachement.ProjetId,
            attachement.Date,
            LibelleStatut(attachement.Statut),
            attachement.Statut,
            lignes.Count,
            lignes.Sum(l => l.MontantCalcule),
            lignes);

    private static decimal CalculerMontant(decimal quantite, decimal prixUnitaireHT) =>
        Math.Round(quantite * prixUnitaireHT, 2);

    private static string LibelleStatut(StatutAttachement statut) => statut switch
    {
        StatutAttachement.Brouillon => "Brouillon",
        StatutAttachement.Contradictoire => "Contradictoire",
        StatutAttachement.Valide => "Validé",
        _ => statut.ToString()
    };
}
