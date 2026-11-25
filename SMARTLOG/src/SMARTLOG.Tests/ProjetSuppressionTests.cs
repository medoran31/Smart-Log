using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Vérifie que la suppression d'un projet emporte toutes ses données liées
/// sans laisser d'orphelins, et qu'elle reste tracée dans le journal.
/// </summary>
public class ProjetSuppressionTests : SqliteTestBase
{
    [Fact]
    public async Task SupprimerAsync_RetireLeProjetEtTousSesEnfants_etJournalise()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-SUP", 100m, 10m);

        var avenant = new Avenant
        {
            MarcheId = marche.Id,
            Numero = 1,
            Type = TypeAvenant.Financier,
            MontantDelta = 1_000m,
            Statut = StatutAvenant.Brouillon
        };
        var attachement = new Attachement
        {
            ProjetId = projet.Id,
            Date = new DateOnly(2026, 3, 1),
            Statut = StatutAttachement.Brouillon
        };
        var parent = new Tache
        {
            ProjetId = projet.Id,
            Libelle = "Tâche parent",
            DateDebutPrevue = new DateOnly(2026, 3, 1),
            DateFinPrevue = new DateOnly(2026, 3, 10)
        };
        var enfant = new Tache
        {
            ProjetId = projet.Id,
            Libelle = "Sous-tâche",
            ParentTache = parent,
            DateDebutPrevue = new DateOnly(2026, 3, 2),
            DateFinPrevue = new DateOnly(2026, 3, 5)
        };
        var liaison = new LiaisonTache
        {
            TachePredecesseur = parent,
            TacheSuccesseur = enfant,
            TypeLiaison = TypeLiaison.FinDebut
        };

        Db.AddRange(
            avenant,
            attachement,
            new LigneAttachement
            {
                AttachementId = attachement.Id,
                PrixUnitaireId = prix.Id,
                QuantiteExecutee = 2m,
                MontantCalcule = 200m
            },
            new Situation
            {
                ProjetId = projet.Id,
                Periode = "2026-03",
                Type = TypeSituation.Provisoire,
                MontantCumule = 200m,
                Statut = StatutSituation.Brouillon
            },
            parent,
            enfant,
            liaison,
            new Reserve
            {
                ProjetId = projet.Id,
                Numero = 1,
                Description = "Réserve à supprimer",
                Priorite = PrioriteReserve.Haute,
                DateConstatation = new DateOnly(2026, 3, 1),
                DelaiLeveeJours = 10,
                Statut = StatutReserve.Nouvelle
            },
            new DocumentGed
            {
                ProjetId = projet.Id,
                TypeDocument = "PDF",
                CheminFichier = Path.Combine(Path.GetTempPath(), "smartlog-suppression-test.pdf"),
                Version = 1
            },
            new Courrier
            {
                ProjetId = projet.Id,
                Sens = SensCourrier.Entrant,
                Expediteur = "Maître d'ouvrage",
                Destinataire = "Entreprise",
                Objet = "Courrier à supprimer",
                DateCourrier = new DateOnly(2026, 3, 1),
                Statut = StatutCourrier.EnAttente
            },
            new Alerte
            {
                ProjetId = projet.Id,
                EntiteSourceType = "Projet",
                EntiteSourceId = projet.Id,
                DateEcheance = new DateOnly(2026, 3, 10),
                Priorite = PrioriteAlerte.Moyenne
            });
        await Db.SaveChangesAsync();

        await Projets.SupprimerAsync(projet.Id, UtilisateurTestId);

        Assert.Null(await Projets.ObtenirAsync(projet.Id));
        Assert.Empty(await Projets.ListerAsync());
        Assert.False(await Db.Marches.AsNoTracking().AnyAsync());
        Assert.False(await Db.Avenants.AsNoTracking().AnyAsync());
        Assert.False(await Db.PrixUnitaires.AsNoTracking().AnyAsync());
        Assert.False(await Db.LignesAttachement.AsNoTracking().AnyAsync());
        Assert.False(await Db.Attachements.AsNoTracking().AnyAsync());
        Assert.False(await Db.Situations.AsNoTracking().AnyAsync());
        Assert.False(await Db.LiaisonsTache.AsNoTracking().AnyAsync());
        Assert.False(await Db.Taches.AsNoTracking().AnyAsync());
        Assert.False(await Db.Reserves.AsNoTracking().AnyAsync());
        Assert.False(await Db.Documents.AsNoTracking().AnyAsync());
        Assert.False(await Db.Courriers.AsNoTracking().AnyAsync());
        Assert.False(await Db.Alertes.AsNoTracking().AnyAsync());

        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        var suppression = Assert.Single(journal, j => j.ActionLibelle == "Suppression de projet");
        Assert.Contains("TEST-001", suppression.ValeurAvantJson!);
        Assert.Null(suppression.ValeurApresJson);
    }

    [Fact]
    public async Task SupprimerAsync_ProjetInexistant_LeveUneException()
    {
        var inconnu = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Projets.SupprimerAsync(inconnu, UtilisateurTestId));
    }
}
