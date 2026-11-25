using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Vérifie la chaîne avenant → finances → planning → alertes et l'absence de
/// valeurs dérivées périmées après une mutation contractuelle.
/// </summary>
public class RecalculCroiseServiceTests : SqliteTestBase
{
    [Fact]
    public async Task AvenantValide_RecalculeProjetFinancesPlanningEtAlertes()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(attachement.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(
            attachement.Id, StatutAttachement.Valide, UtilisateurTestId);
        await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);
        await Planning.CreerTacheAsync(
            projet.Id, null, "Tâche initiale",
            new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 12), UtilisateurTestId);

        var avenant = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(0, TypeAvenant.Mixte, 50_000m, 10), UtilisateurTestId);
        await Avenants.ValiderAsync(avenant.Id, UtilisateurTestId);

        await Recalcul.RecalculerDepuisProjetAsync(projet.Id, UtilisateurTestId);

        var proj = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(projet.MontantInitial + 50_000m, proj.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours + 10, proj.DelaiActualiseJours);

        var suivi = await Situations.ObtenirSuiviAsync(projet.Id);
        Assert.Equal(1_000m, suivi.TotalEngage);
        Assert.Equal(proj.MontantActualise, suivi.MontantContractuel);

        var planning = await Planning.ObtenirAsync(projet.Id);
        Assert.Single(planning.Taches);

        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul croisé (finances, planning, alertes)");
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul du planning");
        var situation = await Db.Situations.AsNoTracking().SingleAsync(s => s.ProjetId == projet.Id);
        var journalSituation = await Journal.ListerAsync(nameof(Situation), situation.Id);
        Assert.Contains(journalSituation, j => j.ActionLibelle == "Recalcul de situation");
    }

    [Fact]
    public async Task ModificationBpu_RecalculeLigneEtSituationSansDupliquerLeCalcul()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        var ligne = await Attachements.AjouterLigneAsync(
            attachement.Id, prix.Id, 10m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(
            attachement.Id, StatutAttachement.Valide, UtilisateurTestId);
        await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.10m, 0m, UtilisateurTestId);

        var modifie = await Marches.ModifierPrixUnitaireAsync(
            new PrixUnitaireModification(
                prix.Id, "PU-01", "Poste PU-01", "m²", 150m, 100m),
            UtilisateurTestId);

        Assert.Equal(150m, modifie.PrixUnitaireHT);
        var lignePersistee = await Db.LignesAttachement.AsNoTracking()
            .SingleAsync(l => l.Id == ligne.Id);
        Assert.Equal(1_500m, lignePersistee.MontantCalcule);

        await Recalcul.RecalculerDepuisProjetAsync(projet.Id, UtilisateurTestId);

        var situation = await Db.Situations.AsNoTracking().SingleAsync();
        Assert.Equal(1_500m, situation.MontantCumule);
        var journalLigne = await Journal.ListerAsync(nameof(LigneAttachement), ligne.Id);
        Assert.Contains(journalLigne, j => j.ActionLibelle == "Recalcul du montant de ligne après BPU");
    }

    [Fact]
    public async Task ModificationDateCommencement_RecalculeDatePrevisionnelleEtPlanning()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        await Planning.CreerTacheAsync(
            projet.Id, null, "Tâche initiale",
            new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 12), UtilisateurTestId);
        var dateInitiale = projet.DatePrevisionnelleAchevement;
        var nouvelleDate = new DateOnly(2026, 3, 1);

        await Projets.ModifierAsync(
            new ProjetModification(
                projet.Id, projet.Intitule, projet.Localisation, projet.NatureTravaux,
                projet.DateNotification, nouvelleDate),
            UtilisateurTestId);

        var proj = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.NotEqual(dateInitiale, proj.DatePrevisionnelleAchevement);
        Assert.Equal(
            nouvelleDate.AddDays(projet.DelaiActualiseJours),
            proj.DatePrevisionnelleAchevement);

        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul des valeurs dérivées");
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul du planning");
    }

    [Fact]
    public async Task CreerOuRecalculerAsync_AvecTauxAvancePersonnalise_CalculeSelonLeTauxSaisi()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var prix = await CreerPrixUnitaireAsync(marche.Id, "PU-01", 100m, 100m);
        var attachement = await Attachements.CreerAsync(
            projet.Id, new DateOnly(2026, 1, 10), UtilisateurTestId);
        await Attachements.AjouterLigneAsync(attachement.Id, prix.Id, 20m, UtilisateurTestId);
        await Attachements.ChangerStatutAsync(
            attachement.Id, StatutAttachement.Valide, UtilisateurTestId);

        var situation = await Situations.CreerOuRecalculerAsync(
            projet.Id, "2026-01", TypeSituation.Provisoire, 0.05m, 0.25m, 0m, UtilisateurTestId);

        Assert.Equal(2_000m, situation.MontantCumule);
        Assert.Equal(500m, situation.Avance);
        Assert.Equal(100m, situation.RetenueGarantie);
    }

    [Fact]
    public async Task RecalculValeursDerivees_EstIdempotentEtJournalise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();

        await Avenants.RecalculerValeursDeriveesAsync(projet.Id, UtilisateurTestId);
        await Avenants.RecalculerValeursDeriveesAsync(projet.Id, UtilisateurTestId);

        var persiste = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(projet.MontantInitial, persiste.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, persiste.DelaiActualiseJours);
        var journal = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Equal(2, journal.Count(j => j.ActionLibelle == "Recalcul des valeurs dérivées"));
    }
}
