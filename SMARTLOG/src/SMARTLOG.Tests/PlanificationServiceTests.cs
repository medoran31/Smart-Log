using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Tests du moteur CPM et du recalcul en cascade. Le scénario principal
/// comporte quatre tâches liées avec un chemin parallèle non critique.
/// </summary>
public class PlanificationServiceTests : SqliteTestBase
{
    private sealed record ScenarioQuatreTaches(
        Guid A, Guid B, Guid C, Guid D, Guid ProjetId, PlanningDetail Planning);

    [Fact]
    public async Task CpmQuatreTachesLiees_DetermineLeCheminCritiqueEtLesMarges()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var taches = await CreerScenarioQuatreTachesAsync(projet.Id);

        var resumes = taches.Planning.Taches.ToDictionary(t => t.Id);
        Assert.Equal(new DateOnly(2026, 1, 9), resumes[taches.D].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 12), resumes[taches.D].DateFinPrevue);
        Assert.Equal(12, taches.Planning.DureeProjetJours);

        Assert.Equal(0, resumes[taches.A].MargeTotale);
        Assert.Equal(0, resumes[taches.B].MargeTotale);
        Assert.Equal(3, resumes[taches.C].MargeTotale);
        Assert.Equal(0, resumes[taches.D].MargeTotale);

        Assert.Equal(new[] { taches.A, taches.B, taches.D }.OrderBy(x => x),
            taches.Planning.TachesCritiques.OrderBy(x => x));
        Assert.DoesNotContain(taches.C, taches.Planning.TachesCritiques);

        var journal = await Journal.ListerAsync(nameof(Projet), taches.ProjetId);
        Assert.Contains(journal, j => j.ActionLibelle == "Recalcul du planning");
    }

    [Fact]
    public async Task ChangementDeDuree_DecaleTousLesSuccesseursEnCascade()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var taches = await CreerScenarioQuatreTachesAsync(projet.Id);

        // A passe de 3 à 5 jours : B, C puis D sont décalés.
        await Planning.ModifierTacheAsync(
            taches.A, " Études allongées ", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), UtilisateurTestId);

        var apres = await Planning.ObtenirAsync(projet.Id);
        var parId = apres.Taches.ToDictionary(t => t.Id);
        Assert.Equal(new DateOnly(2026, 1, 6), parId[taches.B].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 10), parId[taches.B].DateFinPrevue);
        Assert.Equal(new DateOnly(2026, 1, 6), parId[taches.C].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 11), parId[taches.D].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 14), parId[taches.D].DateFinPrevue);
        Assert.Equal("Études allongées", parId[taches.A].Libelle);
    }

    [Fact]
    public async Task ChangementDeDateSurTacheLiee_PropageLaContrainteAuxSuccesseurs()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var taches = await CreerScenarioQuatreTachesAsync(projet.Id);

        // C est liée à A : la nouvelle date est imposée puis propagée à D.
        await Planning.ModifierTacheAsync(
            taches.C, "Finitions", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 11), UtilisateurTestId);

        var apres = await Planning.ObtenirAsync(projet.Id);
        var parId = apres.Taches.ToDictionary(t => t.Id);
        Assert.Equal(new DateOnly(2026, 1, 10), parId[taches.C].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 12), parId[taches.D].DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 1, 15), parId[taches.D].DateFinPrevue);

        var journal = await Journal.ListerAsync(nameof(Tache), taches.C);
        Assert.Contains(journal, j => j.ActionLibelle == "Modification du planning");
    }

    [Fact]
    public async Task LiaisonCreeantUnCycle_EstRefuseeParLeCpm()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var a = await Planning.CreerTacheAsync(
            projet.Id, null, "A", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), UtilisateurTestId);
        var b = await Planning.CreerTacheAsync(
            projet.Id, null, "B", new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), UtilisateurTestId);
        await Planning.AjouterLiaisonAsync(
            a.Id, b.Id, TypeLiaison.FinDebut, UtilisateurTestId);

        // Injection directe pour tester le garde-fou du moteur CPM lui-même.
        Db.LiaisonsTache.Add(new LiaisonTache
        {
            TachePredecesseurId = b.Id,
            TacheSuccesseurId = a.Id,
            TypeLiaison = TypeLiaison.FinDebut
        });
        await Db.SaveChangesAsync();

        var erreur = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Planning.CalculerAsync(projet.Id));
        Assert.Contains("Cycle", erreur.Message);
    }

    [Fact]
    public async Task QuatreTypesDeLiaison_ProduisentLesDatesAttendues()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var p = await Planning.CreerTacheAsync(
            projet.Id, null, "Prédécesseur", new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 14), UtilisateurTestId);

        var types = new[]
        {
            (TypeLiaison.FinDebut, new DateOnly(2026, 1, 15)),
            (TypeLiaison.DebutDebut, new DateOnly(2026, 1, 10)),
            (TypeLiaison.FinFin, new DateOnly(2026, 1, 13)),
            (TypeLiaison.DebutFin, new DateOnly(2026, 1, 9))
        };

        var successeurs = new List<(Guid Id, DateOnly Attendu)>();
        foreach (var (type, attendu) in types)
        {
            var tache = await Planning.CreerTacheAsync(
                projet.Id, null, $"S-{type}", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), UtilisateurTestId);
            await Planning.AjouterLiaisonAsync(p.Id, tache.Id, type, UtilisateurTestId);
            successeurs.Add((tache.Id, attendu));
        }

        var apres = await Planning.ObtenirAsync(projet.Id);
        var parId = apres.Taches.ToDictionary(t => t.Id);
        foreach (var (id, attendu) in successeurs)
            Assert.Equal(attendu, parId[id].DateDebutPrevue);

        Assert.Equal(4, apres.NombreLiaisons);
        Assert.Contains(apres.Liaisons, l => l.TypeLibelle == "Fin → Début");
        Assert.Contains(apres.Liaisons, l => l.TypeLibelle == "Début → Fin");
    }

    [Fact]
    public async Task Wbs_AplatitLesTachesEnPreOrdreAvecLeNiveau()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var lot = await Planning.CreerTacheAsync(
            projet.Id, null, "Lot 1", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 10), UtilisateurTestId);
        var fondations = await Planning.CreerTacheAsync(
            projet.Id, lot.Id, "Fondations", new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 4), UtilisateurTestId);
        var coffrage = await Planning.CreerTacheAsync(
            projet.Id, fondations.Id, "Coffrage", new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 3), UtilisateurTestId);
        var grosOeuvre = await Planning.CreerTacheAsync(
            projet.Id, lot.Id, "Gros œuvre", new DateOnly(2026, 2, 5), new DateOnly(2026, 2, 10), UtilisateurTestId);

        var apres = await Planning.ObtenirAsync(projet.Id);
        var parId = apres.Taches.ToDictionary(t => t.Id);

        Assert.Equal(0, parId[lot.Id].Niveau);
        Assert.Equal(1, parId[fondations.Id].Niveau);
        Assert.Equal(2, parId[coffrage.Id].Niveau);
        Assert.Equal(1, parId[grosOeuvre.Id].Niveau);
        Assert.Equal(
            new[] { lot.Id, fondations.Id, coffrage.Id, grosOeuvre.Id },
            apres.Taches.Select(t => t.Id));
    }

    [Fact]
    public async Task SuppressionTacheLiee_EstRefuseeTantQueLaLiaisonExiste()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var a = await Planning.CreerTacheAsync(
            projet.Id, null, "A", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), UtilisateurTestId);
        var b = await Planning.CreerTacheAsync(
            projet.Id, null, "B", new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), UtilisateurTestId);
        var liaison = await Planning.AjouterLiaisonAsync(
            a.Id, b.Id, TypeLiaison.FinDebut, UtilisateurTestId);

        var erreur = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Planning.SupprimerTacheAsync(a.Id, UtilisateurTestId));
        Assert.Contains("liée", erreur.Message);

        await Planning.SupprimerLiaisonAsync(liaison.Id, UtilisateurTestId);
        await Planning.SupprimerTacheAsync(a.Id, UtilisateurTestId);
        Assert.Equal(1, (await Planning.ObtenirAsync(projet.Id)).NombreTaches);
    }

    private async Task<ScenarioQuatreTaches> CreerScenarioQuatreTachesAsync(Guid projetId)
    {
        var a = await Planning.CreerTacheAsync(
            projetId, null, "Études", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), UtilisateurTestId);
        var b = await Planning.CreerTacheAsync(
            projetId, null, "Gros œuvre", new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 8), UtilisateurTestId);
        var c = await Planning.CreerTacheAsync(
            projetId, null, "Réseau", new DateOnly(2026, 1, 4), new DateOnly(2026, 1, 5), UtilisateurTestId);
        var d = await Planning.CreerTacheAsync(
            projetId, null, "Livraison", new DateOnly(2026, 1, 9), new DateOnly(2026, 1, 12), UtilisateurTestId);

        await Planning.AjouterLiaisonAsync(a.Id, b.Id, TypeLiaison.FinDebut, UtilisateurTestId);
        await Planning.AjouterLiaisonAsync(a.Id, c.Id, TypeLiaison.FinDebut, UtilisateurTestId);
        await Planning.AjouterLiaisonAsync(b.Id, d.Id, TypeLiaison.FinDebut, UtilisateurTestId);
        await Planning.AjouterLiaisonAsync(c.Id, d.Id, TypeLiaison.FinDebut, UtilisateurTestId);

        var planning = await Planning.CalculerAsync(projetId);
        return new ScenarioQuatreTaches(a.Id, b.Id, c.Id, d.Id, projetId, planning);
    }
}
