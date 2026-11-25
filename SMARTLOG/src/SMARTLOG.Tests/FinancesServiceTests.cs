using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Tests;

/// <summary>
/// Tests des calculs financiers : chaque montant dérivé doit provenir des
/// services et jamais d'une saisie directe dans les entités.
/// </summary>
public class FinancesServiceTests : SqliteTestBase
{
    [Fact]
    public async Task AjouterLigne_CalculeLeMontantEtJournaliseLajout()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 185m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 15), UtilisateurTestId);

        var ligne = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 12.5m, UtilisateurTestId);

        Assert.Equal(12.5m, ligne.QuantiteExecutee);
        Assert.Equal(2_312.50m, ligne.MontantCalcule);
        Assert.False(ligne.DepasseReliquat);
        Assert.Equal(87.5m, ligne.ReliquatApresLigne);

        var persiste = await Db.LignesAttachement.AsNoTracking().SingleAsync(l => l.Id == ligne.Id);
        Assert.Equal(2_312.50m, persiste.MontantCalcule);

        var journal = await Journal.ListerAsync(nameof(LigneAttachement), ligne.Id);
        Assert.Single(journal);
        Assert.Equal("Ajout de ligne", journal[0].ActionLibelle);
    }

    [Fact]
    public async Task ModifierQuantite_RecalculeLeMontantEtConserveLavantDansLeJournal()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 185m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 15), UtilisateurTestId);
        var ligne = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 10m, UtilisateurTestId);

        var modifiee = await Attachements.ModifierQuantiteAsync(
            ligne.Id, 20m, UtilisateurTestId);

        Assert.Equal(20m, modifiee.QuantiteExecutee);
        Assert.Equal(3_700m, modifiee.MontantCalcule);

        var journal = await Journal.ListerAsync(nameof(LigneAttachement), ligne.Id);
        Assert.Equal(2, journal.Count);
        Assert.Equal("Modification de quantité", journal[0].ActionLibelle);
        Assert.Contains("10", journal[0].ValeurAvantJson ?? string.Empty);
        Assert.Contains("3700", journal[0].ValeurApresJson ?? string.Empty);
    }

    [Fact]
    public async Task QuantiteSuperieureAuReliquat_EstSignaleeSansBloquerLaSaisie()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 185m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 15), UtilisateurTestId);

        await Attachements.AjouterLigneAsync(attachement.Id, prix.Id, 80m, UtilisateurTestId);
        var depassement = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 30m, UtilisateurTestId);

        Assert.True(depassement.DepasseReliquat);
        Assert.Equal(80m, depassement.QuantiteDejaExecuteeAilleurs);
        Assert.Equal(20m, depassement.ReliquatAvantLigne);
        Assert.Equal(-10m, depassement.ReliquatApresLigne);
        Assert.Equal(5_550m, depassement.MontantCalcule);

        // L'aperçu temps réel utilise le même calcul, sans écriture.
        var simulation = await Attachements.SimulerLigneAsync(projet.Id, prix.Id, 30m, depassement.Id);
        Assert.True(simulation.DepasseReliquat);
        Assert.Equal(-10m, simulation.ReliquatApresLigne);
        Assert.Equal(0, await Db.LignesAttachement.CountAsync(l => l.Id == simulation.Id));
    }

    [Fact]
    public async Task Situation_CumuleLesAttachementsValidesJusquaLaPeriode()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);

        var janvier = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        var ligneJanvier = await Attachements.AjouterLigneAsync(janvier.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(janvier.Id, StatutAttachement.Valide, UtilisateurTestId);

        var fevrier = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 2, 10), UtilisateurTestId);
        var ligneFevrier = await Attachements.AjouterLigneAsync(fevrier.Id, prix.Id, 2m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(fevrier.Id, StatutAttachement.Valide, UtilisateurTestId);

        var situationJanvier = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);
        var situationFevrier = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-02", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);

        Assert.Equal(1_000m, situationJanvier.MontantPeriode);
        Assert.Equal(1_000m, situationJanvier.MontantCumule);
        Assert.Equal(200m, situationFevrier.MontantPeriode);
        Assert.Equal(1_200m, situationFevrier.MontantCumule);

        Assert.True(ligneJanvier.MontantCalcule > 0m);
        Assert.True(ligneFevrier.MontantCalcule > 0m);

        var journal = await Journal.ListerAsync(nameof(Situation), situationFevrier.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul de situation");
    }

    [Fact]
    public async Task Initialiser_CalculeLaRetenueDeGarantieSurLeMontantActualise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync(montant: 1_250_000m);
        var situation = new Situation
        {
            ProjetId = projet.Id,
            Periode = "2026-01",
            Type = TypeSituation.Provisoire
        };
        Db.Situations.Add(situation);
        await Db.SaveChangesAsync();

        await Situations.InitialiserAsync(situation.Id, 0.35m, 0.05m, UtilisateurTestId);

        var persiste = await Db.Situations.AsNoTracking().SingleAsync(s => s.Id == situation.Id);
        Assert.Equal(437_500m, persiste.MontantCumule);
        Assert.Equal(21_875m, persiste.RetenueGarantie);
        Assert.Equal(43_750m, persiste.Avance);
        Assert.Equal(0m, persiste.Penalites);

        // Idempotence : un second appel ne modifie rien.
        await Situations.InitialiserAsync(situation.Id, 0.99m, 0.99m, UtilisateurTestId);
        var apres = await Db.Situations.AsNoTracking().SingleAsync(s => s.Id == situation.Id);
        Assert.Equal(437_500m, apres.MontantCumule);
    }

    [Fact]
    public async Task Recalculer_AppliqueRetenueAvanceEtPenalitesEtCalculeLeNet()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 150m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 3, 5), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(attachement.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(attachement.Id, StatutAttachement.Valide, UtilisateurTestId);

        var situation = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-03", TypeSituation.Provisoire, 0.05m, 0.10m, 25m, UtilisateurTestId);

        Assert.Equal(1_500m, situation.MontantCumule);
        Assert.Equal(75m, situation.RetenueGarantie);
        Assert.Equal(150m, situation.Avance);
        Assert.Equal(25m, situation.Penalites);
        Assert.Equal(1_250m, situation.NetAPayer);
    }

    [Fact]
    public async Task AttachementNonValide_NePasAlimenterLeCumul()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);

        var valide = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(valide.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(valide.Id, StatutAttachement.Valide, UtilisateurTestId);

        var brouillon = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 20), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(brouillon.Id, prix.Id, 5m, UtilisateurTestId);

        var situation = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);

        Assert.Equal(1_000m, situation.MontantCumule);
        Assert.Equal(1_000m, situation.MontantPeriode);
    }

    [Fact]
    public async Task SuiviFinancier_SuitEngagePayeEtRestantDuDansLeTemps()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync(montant: 1_250_000m);
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);

        var janvier = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(janvier.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(janvier.Id, StatutAttachement.Valide, UtilisateurTestId);

        var fevrier = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 2, 10), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(fevrier.Id, prix.Id, 2m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(fevrier.Id, StatutAttachement.Valide, UtilisateurTestId);

        var s1 = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);
        var s2 = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-02", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);
        await Situations.ChangerStatutAsync(s1.Id, StatutSituation.Payee, UtilisateurTestId);

        var suivi = await Situations.ObtenirSuiviAsync(projet.Id);

        Assert.Equal(2, suivi.Evolution.Count);
        Assert.Equal(1_000m, suivi.Evolution[0].MontantEngage);
        Assert.Equal(1_200m, suivi.Evolution[1].MontantEngage);
        Assert.Equal(850m, suivi.Evolution[0].MontantPaye);
        Assert.Equal(850m, suivi.Evolution[1].MontantPaye);
        Assert.Equal(150m, suivi.Evolution[0].RestantDu);
        Assert.Equal(350m, suivi.Evolution[1].RestantDu);
        Assert.Equal(1_200m, suivi.TotalEngage);
        Assert.Equal(850m, suivi.TotalPaye);
        Assert.Equal(350m, suivi.TotalRestantDu);
        Assert.Equal(0.001m, suivi.TauxAvancement);
        Assert.Equal(850m, s1.NetAPayer);
        Assert.Equal(1_020m, s2.NetAPayer);
    }
}
