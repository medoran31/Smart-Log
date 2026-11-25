using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;
using Smartlog.Services.Ged;

namespace Smartlog.Tests;

/// <summary>Tests du cycle de vie des réserves, du registre courrier et du moteur d'alertes.</summary>
public class SuiviServiceTests : SqliteTestBase
{
    [Fact]
    public async Task CycleDeVieReserve_GenereUnPvLieEtJournaliseChaqueEtape()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var aujourdHui = DateOnly.FromDateTime(DateTime.Now);
        var reserve = await Reserves.CreerAsync(
            projet.Id,
            "Fuite sur le circuit d'alimentation",
            PrioriteReserve.Haute,
            null,
            aujourdHui.AddDays(-30),
            10,
            UtilisateurTestId);

        Assert.Equal(1, reserve.Numero);
        Assert.Equal("Réserve n°1", reserve.Libelle);
        Assert.Equal(aujourdHui.AddDays(-20), reserve.DateEcheanceLevee);
        Assert.True(reserve.EstEnRetard);
        Assert.Equal("Nouvelle", reserve.StatutLibelle);

        var sautInterdit = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Reserves.ChangerStatutAsync(
                reserve.Id, StatutReserve.Levee, UtilisateurTestId));
        Assert.Contains("Transition interdite", sautInterdit.Message);

        await Reserves.ChangerStatutAsync(reserve.Id, StatutReserve.EnCours, UtilisateurTestId);
        await Reserves.ChangerStatutAsync(reserve.Id, StatutReserve.EnVerification, UtilisateurTestId);
        var levee = await Reserves.ChangerStatutAsync(
            reserve.Id, StatutReserve.Levee, UtilisateurTestId);

        Assert.Equal(StatutReserve.Levee, levee.Statut);
        Assert.NotNull(levee.DateLevee);
        Assert.False(levee.EstEnRetard);
        Assert.NotNull(levee.CheminPvLevee);

        var documents = await Ged.ListerAsync(projet.Id);
        var pv = Assert.Single(documents, d => d.EntiteLieeType == nameof(Reserve));
        Assert.Equal(reserve.Id, pv.EntiteLieeId);
        Assert.StartsWith("PV de levée", pv.NomFichier);

        var journal = await Journal.ListerAsync(nameof(Reserve), reserve.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Génération du PV de levée");
        Assert.Contains(journal, j => j.ActionLibelle == "Changement de statut");

        var cloturee = await Reserves.ChangerStatutAsync(
            reserve.Id, StatutReserve.Cloturee, UtilisateurTestId);
        Assert.Equal("Clôturée", cloturee.StatutLibelle);
        Assert.Equal(0, cloturee.JoursRestants + 20); // l'ancienneté reste calculée
    }

    [Fact]
    public async Task Courrier_CalculeLeDelaiDeReponseEtEnregistreLaReponse()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var aujourdHui = DateOnly.FromDateTime(DateTime.Now);
        var courrier = await Courriers.CreerAsync(
            projet.Id,
            SensCourrier.Entrant,
            "Maître d'ouvrage",
            "Maîtrise d'œuvre",
            "Observations sur le Planning",
            aujourdHui.AddDays(-5),
            3,
            UtilisateurTestId);

        Assert.Equal("Entrant", courrier.SensLibelle);
        Assert.Equal("En attente", courrier.StatutLibelle);
        Assert.Equal(aujourdHui.AddDays(-2), courrier.DateEcheanceReponse);
        Assert.Equal(-2, courrier.JoursRestants);
        Assert.True(courrier.EstEnRetard);

        var repondu = await Courriers.EnregistrerReponseAsync(
            courrier.Id, aujourdHui, UtilisateurTestId);
        Assert.Equal(StatutCourrier.Repondu, repondu.Statut);
        Assert.Equal(aujourdHui, repondu.DateReponse);
        Assert.False(repondu.EstEnRetard);

        var journal = await Journal.ListerAsync(nameof(Courrier), courrier.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Enregistrement de la réponse");

        var sansDelai = await Courriers.CreerAsync(
            projet.Id, SensCourrier.Sortant, "Maîtrise d'œuvre", "Entreprise",
            "Transmission du dossier", aujourdHui, null, UtilisateurTestId);
        Assert.Null(sansDelai.DateEcheanceReponse);
        Assert.Null(sansDelai.JoursRestants);

        var sansSuite = await Courriers.MarquerSansSuiteAsync(
            sansDelai.Id, UtilisateurTestId);
        Assert.Equal("Sans suite", sansSuite.StatutLibelle);
    }

    [Fact]
    public async Task MoteurAlertes_DetecteLesQuatreReglesEtResteIdempotent()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var aujourdHui = DateOnly.FromDateTime(DateTime.Now);

        var entite = await Db.Projets.SingleAsync(p => p.Id == projet.Id);
        entite.DatePrevisionnelleAchevement = aujourdHui.AddDays(20);
        await Db.SaveChangesAsync();

        var tache = await Planning.CreerTacheAsync(
            projet.Id, null, "Tâche en retard", aujourdHui.AddDays(-10),
            aujourdHui.AddDays(-5), UtilisateurTestId);
        var reserve = await Reserves.CreerAsync(
            projet.Id, "Réserve non levée", PrioriteReserve.Haute, null,
            aujourdHui.AddDays(-20), 10, UtilisateurTestId);
        var courrier = await Courriers.CreerAsync(
            projet.Id, SensCourrier.Entrant, "MOA", "MOE", "Réponse attendue",
            aujourdHui.AddDays(-10), 5, UtilisateurTestId);

        var centre = await Alertes.AnalyserAsync(projet.Id, UtilisateurTestId, 30);

        Assert.Equal(4, centre.Alertes.Count);
        Assert.Equal(4, centre.NombreNonLues);
        Assert.Equal(3, centre.NombreEnRetard);
        Assert.Equal(
            new[] { nameof(Projet), nameof(Tache), nameof(Reserve), nameof(Courrier) }.OrderBy(x => x),
            centre.Alertes.Select(a => a.SourceType).OrderBy(x => x));
        Assert.Contains(centre.Alertes, a => a.SourceId == tache.Id && a.MenuCible == "Planning");
        Assert.Contains(centre.Alertes, a => a.SourceId == reserve.Id && a.MenuCible == "Réserves");
        Assert.Contains(centre.Alertes, a => a.SourceId == courrier.Id && a.MenuCible == "Courrier");
        Assert.All(centre.Alertes, a => Assert.False(string.IsNullOrWhiteSpace(a.Titre)));

        var analyseJournal = await Journal.ListerAsync(nameof(Alerte), Guid.Empty);
        Assert.Contains(analyseJournal, j => j.ActionLibelle == "Analyse des alertes");

        // Un second passage ne duplique pas les alertes.
        var second = await Alertes.AnalyserAsync(projet.Id, UtilisateurTestId, 30);
        Assert.Equal(4, second.Alertes.Count);
        Assert.Equal(4, await Db.Alertes.CountAsync());

        var premiere = second.Alertes.First(a => a.SourceType == nameof(Tache));
        await Alertes.MarquerLueAsync(premiere.Id, UtilisateurTestId);
        Assert.Equal(3, (await Alertes.ObtenirCentreAsync(projet.Id)).NombreNonLues);

        await Alertes.MarquerToutesLuesAsync(projet.Id, UtilisateurTestId);
        Assert.Equal(0, (await Alertes.ObtenirCentreAsync(projet.Id)).NombreNonLues);

        // Sans changement d'échéance, une nouvelle analyse conserve le statut lu.
        await Alertes.AnalyserAsync(projet.Id, UtilisateurTestId, 30);
        Assert.Equal(0, (await Alertes.ObtenirCentreAsync(projet.Id)).NombreNonLues);
    }
}
