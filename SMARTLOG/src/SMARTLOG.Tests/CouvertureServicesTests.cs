using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;
using Smartlog.Services.Ged;

namespace Smartlog.Tests;

/// <summary>
/// Couverture des méthodes de lecture et de mutation encore non exercées :
/// chaque service est testé via son interface, jamais par recalcul dans la vue.
/// </summary>
public class CouvertureServicesTests : SqliteTestBase
{
    [Fact]
    public async Task ListerAvenants_RetourneLesBrouillonsEtValidesDansLOrdre()
    {
        var (_, marche) = await CreerProjetEtMarcheAsync();
        var premier = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(1, TypeAvenant.Financier, 10_000m, 0), UtilisateurTestId);
        var deuxieme = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(2, TypeAvenant.Delai, 0m, 5), UtilisateurTestId);

        await Avenants.ValiderAsync(premier.Id, UtilisateurTestId);

        var avenants = await Avenants.ListerAsync(marche.Id);
        Assert.Equal(2, avenants.Count);
        Assert.Equal(new[] { 1, 2 }, avenants.Select(a => a.Numero));
        Assert.Equal(StatutAvenant.Valide, avenants[0].Statut);
        Assert.Equal(StatutAvenant.Brouillon, avenants[1].Statut);
        Assert.Equal("Financier", avenants[0].TypeLibelle);
        Assert.NotNull(avenants[0].DateValidation);
        Assert.Null(avenants[1].DateValidation);
    }

    [Fact]
    public async Task SupprimerLigne_RecalculeLeReliquatEtJournaliseLaSuppression()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync(montant: 50_000m);
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        var premiere = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 10m, UtilisateurTestId);
        var seconde = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 20m, UtilisateurTestId);

        await Attachements.SupprimerLigneAsync(seconde.Id, UtilisateurTestId);

        var lignes = await Attachements.ListerLignesAsync(projet.Id);
        var restante = Assert.Single(lignes);
        Assert.Equal(premiere.Id, restante.Id);
        Assert.Equal(10m, restante.QuantiteExecutee);
        Assert.Equal(90m, restante.ReliquatApresLigne);
        Assert.Null(await Db.LignesAttachement.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == seconde.Id));
        var journal = await Journal.ListerAsync(nameof(LigneAttachement), seconde.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Suppression de ligne");
    }

    [Fact]
    public async Task ObtenirSituation_RetourneLeResumeCalculeEtNullSiInconnue()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 120m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 4, 8), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(attachement.Id, prix.Id, 5m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(
            attachement.Id, StatutAttachement.Valide, UtilisateurTestId);
        var situation = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-04", TypeSituation.Definitive, 0.05m, 0.10m, 25m, UtilisateurTestId);

        var obtenue = await Situations.ObtenirAsync(situation.Id);

        Assert.NotNull(obtenue);
        Assert.Equal("2026-04", obtenue.Periode);
        Assert.Equal("Définitive", obtenue.TypeLibelle);
        Assert.Equal(600m, obtenue.MontantPeriode);
        Assert.Equal(600m, obtenue.MontantCumule);
        Assert.Equal(30m, obtenue.RetenueGarantie);
        Assert.Equal(60m, obtenue.Avance);
        Assert.Equal(25m, obtenue.Penalites);
        Assert.Equal(485m, obtenue.NetAPayer);
        Assert.Null(await Situations.ObtenirAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RecalculerPlanning_PersisteLesDatesEtLeCheminCritique()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var premiere = await Planning.CreerTacheAsync(
            projet.Id, null, "Préparation", new DateOnly(2026, 5, 2),
            new DateOnly(2026, 5, 6), UtilisateurTestId);
        var seconde = await Planning.CreerTacheAsync(
            projet.Id, null, "Réalisation", new DateOnly(2026, 5, 7),
            new DateOnly(2026, 5, 17), UtilisateurTestId);
        await Planning.AjouterLiaisonAsync(
            premiere.Id, seconde.Id, TypeLiaison.FinDebut, UtilisateurTestId);

        var recalcule = await Planning.RecalculerAsync(projet.Id, UtilisateurTestId);
        var persistant = await Planning.ObtenirAsync(projet.Id);

        Assert.Equal(recalcule.NombreTaches, persistant.NombreTaches);
        Assert.Equal(recalcule.NombreLiaisons, persistant.NombreLiaisons);
        Assert.Equal(recalcule.DateFinProjet, persistant.DateFinProjet);
        Assert.Equal(
            recalcule.Taches.OrderBy(t => t.Libelle).Select(t => t.DateFinPrevue),
            persistant.Taches.OrderBy(t => t.Libelle).Select(t => t.DateFinPrevue));
        Assert.All(recalcule.TachesCritiques, id =>
            Assert.Contains(id, persistant.TachesCritiques));
        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul du planning");
    }

    [Fact]
    public async Task ModifierReserve_MetAJourEcheanceEtFiltreLaListe()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var reserve = await Reserves.CreerAsync(
            projet.Id, "Réserve initiale", PrioriteReserve.Moyenne, null,
            new DateOnly(2026, 6, 1), 10, UtilisateurTestId);

        var modifiee = await Reserves.ModifierAsync(
            reserve.Id, "Réspecter le calfeutrement", PrioriteReserve.Haute, null,
            new DateOnly(2026, 6, 10), 5, UtilisateurTestId);

        Assert.Equal("Réspecter le calfeutrement", modifiee.Description);
        Assert.Equal(PrioriteReserve.Haute, modifiee.Priorite);
        Assert.Equal(new DateOnly(2026, 6, 15), modifiee.DateEcheanceLevee);
        Assert.Equal(reserve.Numero, modifiee.Numero);
        Assert.NotNull(await Reserves.ObtenirAsync(reserve.Id));
        Assert.Null(await Reserves.ObtenirAsync(Guid.NewGuid()));

        var duProjet = await Reserves.ListerAsync(projet.Id);
        Assert.Single(duProjet);
        Assert.Empty(await Reserves.ListerAsync(Guid.NewGuid()));
        var journal = await Journal.ListerAsync(nameof(Reserve), reserve.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Modification");
    }

    [Fact]
    public async Task ListerCourriers_FiltreParSensEtParStatut()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var entrant = await Courriers.CreerAsync(
            projet.Id, SensCourrier.Entrant, "MOA", "MOE", "Observations",
            new DateOnly(2026, 7, 1), 5, UtilisateurTestId);
        var sortant = await Courriers.CreerAsync(
            projet.Id, SensCourrier.Sortant, "MOE", "Entreprise", "Ordre de service",
            new DateOnly(2026, 7, 2), null, UtilisateurTestId);
        await Courriers.EnregistrerReponseAsync(
            sortant.Id, new DateOnly(2026, 7, 4), UtilisateurTestId);

        Assert.Equal(2, (await Courriers.ListerAsync(projet.Id)).Count);
        var entrants = await Courriers.ListerAsync(projet.Id, SensCourrier.Entrant);
        Assert.Equal(entrant.Id, Assert.Single(entrants).Id);
        var enAttente = await Courriers.ListerAsync(projet.Id, enAttenteSeulement: true);
        Assert.Equal(entrant.Id, Assert.Single(enAttente).Id);
        Assert.Empty(await Courriers.ListerAsync(Guid.NewGuid()));

        var obtenu = await Courriers.ObtenirAsync(entrant.Id);
        Assert.NotNull(obtenu);
        Assert.Equal("Observations", obtenu.Objet);
        Assert.Null(await Courriers.ObtenirAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ObtenirDocumentGed_RetourneLeResumeStocke()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var cheminSource = Path.Combine(
            Path.GetTempPath(), $"smartlog-couverture-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(cheminSource, "%PDF-1.4 couverture");
        try
        {
            var document = await Ged.ImporterAsync(
                new DemandeImportDocument(projet.Id, cheminSource), UtilisateurTestId);

            var obtenu = await Ged.ObtenirAsync(document.Id);

            Assert.NotNull(obtenu);
            Assert.Equal(document.NomFichier, obtenu.NomFichier);
            Assert.Equal("PDF", obtenu.Categorie);
            Assert.Equal(projet.Id, obtenu.ProjetId);
            Assert.Null(await Ged.ObtenirAsync(Guid.NewGuid()));
            File.Delete(document.CheminFichier);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    [Fact]
    public async Task ProjetService_LireEtChangerStatut_JournaliseSansToucherAuxValeursDerivees()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var montant = projet.MontantActualise;

        var projets = await Projets.ListerAsync();
        Assert.Contains(projets, p => p.Id == projet.Id);
        var obtenu = await Projets.ObtenirAsync(projet.Id);
        Assert.NotNull(obtenu);
        Assert.Equal("En cours", obtenu.StatutLibelle);
        Assert.Null(await Projets.ObtenirAsync(Guid.NewGuid()));

        var modifie = await Projets.ChangerStatutAsync(
            projet.Id, StatutProjet.Suspendu, UtilisateurTestId);

        Assert.Equal("Suspendu", modifie.StatutLibelle);
        Assert.Equal(montant, modifie.MontantActualise);
        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Changement de statut");
    }
}
