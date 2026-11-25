using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.Tests;

public class AvenantServiceTests : SqliteTestBase
{
    [Fact]
    public async Task ValiderPuisAnnulerUnAvenant_RetablitExactementMontantEtDelaiInitiaux()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();

        var avenant = await Avenants.CreerAsync(
            marche.Id,
            new AvenantCreation(0, TypeAvenant.Mixte, 85_000m, 21),
            UtilisateurTestId);

        // Le brouillon n'a aucun effet sur le projet.
        var avantValidation = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(projet.MontantInitial, avantValidation.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, avantValidation.DelaiActualiseJours);

        var valide = await Avenants.ValiderAsync(avenant.Id, UtilisateurTestId);
        Assert.Equal(projet.MontantInitial + 85_000m, valide.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours + 21, valide.DelaiActualiseJours);

        var annule = await Avenants.AnnulerAsync(avenant.Id, UtilisateurTestId);
        Assert.Equal(projet.MontantInitial, annule.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, annule.DelaiActualiseJours);
        Assert.Equal(
            projet.DateCommencement!.Value.AddDays(projet.DelaiContractuelJours),
            annule.DatePrevisionnelleAchevement);

        var persiste = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(projet.MontantInitial, persiste.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, persiste.DelaiActualiseJours);

        var historique = await Journal.ListerAsync(nameof(Projet), projet.Id);
        Assert.Contains(historique, e => e.ActionLibelle == "Initialisation des valeurs dérivées");
        Assert.Contains(historique, e => e.ActionLibelle == "Recalcul suite à avenant");
        Assert.Contains(historique, e => e.ActionLibelle == "Annulation d'avenant");

        var avenantPersiste = await Db.Avenants.AsNoTracking().SingleAsync(a => a.Id == avenant.Id);
        Assert.Equal(StatutAvenant.Annule, avenantPersiste.Statut);
    }

    [Fact]
    public async Task AnnulerUnAvenantParmiPlusieursValides_RecalculeDepuisLesAutresAvenants()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync(780_000m, 210);

        var premier = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(0, TypeAvenant.Financier, 20_000m, 0), UtilisateurTestId);
        var deuxieme = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(0, TypeAvenant.Delai, 0m, 15), UtilisateurTestId);
        var troisieme = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(0, TypeAvenant.Mixte, 5_000m, 5), UtilisateurTestId);

        await Avenants.ValiderAsync(premier.Id, UtilisateurTestId);
        await Avenants.ValiderAsync(deuxieme.Id, UtilisateurTestId);

        var apresAnnulation = await Avenants.AnnulerAsync(deuxieme.Id, UtilisateurTestId);

        Assert.Equal(projet.MontantInitial + 20_000m, apresAnnulation.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, apresAnnulation.DelaiActualiseJours);

        // Le troisième avenant reste en brouillon : aucun effet.
        var brouillon = await Db.Avenants.AsNoTracking().SingleAsync(a => a.Id == troisieme.Id);
        Assert.Equal(StatutAvenant.Brouillon, brouillon.Statut);
    }

    [Fact]
    public async Task SimulerUnAvenant_RenvoieLesValeursAvantApresSansEcrireDansLaBase()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var nombreJournalAvant = await Db.JournalActions.CountAsync();
        var nombreAvenantsAvant = await Db.Avenants.CountAsync();

        var simulation = await Avenants.SimulerAsync(marche.Id, 42_500m, 10, CancellationToken.None);

        Assert.Equal(projet.MontantInitial, simulation.MontantActuel);
        Assert.Equal(projet.MontantInitial + 42_500m, simulation.NouveauMontant);
        Assert.Equal(projet.DelaiContractuelJours, simulation.DelaiActuelJours);
        Assert.Equal(projet.DelaiContractuelJours + 10, simulation.NouveauDelaiJours);
        Assert.Equal(
            projet.DateCommencement!.Value.AddDays(projet.DelaiContractuelJours),
            simulation.DateFinActuelle);
        Assert.Equal(
            projet.DateCommencement.Value.AddDays(projet.DelaiContractuelJours + 10),
            simulation.NouvelleDateFin);

        Assert.Equal(nombreJournalAvant, await Db.JournalActions.CountAsync());
        Assert.Equal(nombreAvenantsAvant, await Db.Avenants.CountAsync());

        var projetApres = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(projet.MontantInitial, projetApres.MontantActualise);
        Assert.Equal(projet.DelaiContractuelJours, projetApres.DelaiActualiseJours);
    }

    [Fact]
    public async Task CreerUnAvenantBrouillon_JournaliseLaCreationSansToucherAuProjet()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var montantAvant = projet.MontantActualise;

        var avenant = await Avenants.CreerAsync(
            marche.Id, new AvenantCreation(1, TypeAvenant.Delai, 0m, 30), UtilisateurTestId);

        Assert.Equal(1, avenant.Numero);
        Assert.Equal("Délai", avenant.TypeLibelle);
        Assert.Equal("Brouillon", avenant.StatutLibelle);

        var projetApres = await Db.Projets.AsNoTracking().SingleAsync(p => p.Id == projet.Id);
        Assert.Equal(montantAvant, projetApres.MontantActualise);

        var journal = await Journal.ListerAsync(nameof(Avenant), avenant.Id);
        Assert.Single(journal);
        Assert.Equal("Création", journal[0].ActionLibelle);
    }
}
