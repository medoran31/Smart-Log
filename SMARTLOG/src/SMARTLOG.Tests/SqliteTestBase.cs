using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;
using Smartlog.Services.Ged;

namespace Smartlog.Tests;

/// <summary>OCR de test deterministic : aucun exécutable externe requis.</summary>
public sealed class OcrFactice : IOcrService
{
    public bool EstDisponible => true;
    public string? TexteImage { get; set; }
    public string? TextePdf { get; set; }
    public bool LeverException { get; set; }

    public Task<string?> ExtraireTexteAsync(string cheminImage, CancellationToken ct = default) =>
        Task.FromResult(LeverException
            ? throw new InvalidOperationException("Tesseract simulé en panne")
            : TexteImage);

    public Task<string?> ExtraireTextePdfAsync(string cheminPdf, CancellationToken ct = default) =>
        Task.FromResult(LeverException
            ? throw new InvalidOperationException("Tesseract simulé en panne")
            : TextePdf);
}

/// <summary>Configuration IA de test : clé factice, aucun accès au disque.</summary>
public sealed class ConfigurationAssistantFactice : IAssistantIaConfiguration
{
    public string? CleApi { get; set; } = "cle-de-test";
    public string Modele { get; set; } = "claude-opus-5-5";
    public string VersionApi { get; set; } = "2026-10-21";
    public string? IdentifiantEspaceTravail { get; set; }
    public string CheminFichier => "(configuration de test)";
    public bool EstConfigure => !string.IsNullOrWhiteSpace(CleApi);
    public string Resumer() => EstConfigure ? "Claude configuré (test)" : "Claude non configuré (test)";
}

/// <summary>Client Claude deterministic : aucune requête réseau.</summary>
public sealed class ClientClaudeFactice : IClientClaude
{
    public string ReponseJson { get; set; } =
        """{"reponse":"[DOC] Fait issu du document.\n[CALCUL] Montant actualisé : 1 250 000 DA.\n[SUGGESTION] Vérifier les avenants.","suggestions":[]}""";

    public int NombreAppels { get; private set; }
    public ClaudeRequete? DerniereRequete { get; private set; }
    public Exception? Exception { get; set; }

    public Task<string> EnvoyerAsync(ClaudeRequete requete, CancellationToken ct = default)
    {
        NombreAppels++;
        DerniereRequete = requete;
        if (Exception is not null)
            throw Exception;
        return Task.FromResult(ReponseJson);
    }
}

/// <summary>
/// Base de tests isolée : une base SQLite en mémoire par test, avec le
/// schéma issu de la migration Initial et un utilisateur pour la traçabilité.
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
    protected static readonly Guid UtilisateurTestId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    private readonly SqliteConnection _connexion;

    protected SmartlogDbContext Db { get; }
    protected JournalService Journal { get; }
    protected AvenantService Avenants { get; }
    protected ProjetService Projets { get; }
    protected MarcheService Marches { get; }
    protected OcrFactice Ocr { get; } = new();
    protected GedService Ged { get; }
    protected ModeleDocumentService Modeles { get; }
    protected AttachementService Attachements { get; }
    protected SituationService Situations { get; }
    protected PlanificationService Planning { get; }
    protected ReserveService Reserves { get; }
    protected CourrierService Courriers { get; }
    protected AlerteService Alertes { get; }
    protected RecalculCroiseService Recalcul { get; }
    protected ClientClaudeFactice Claude { get; } = new();
    protected ConfigurationAssistantFactice ConfigurationAssistant { get; } = new();
    protected AssistantIaService AssistantIa { get; }

    protected SqliteTestBase()
    {
        _connexion = new SqliteConnection("Data Source=:memory:");
        _connexion.Open();

        var options = new DbContextOptionsBuilder<SmartlogDbContext>()
            .UseSqlite(_connexion)
            .Options;

        Db = new SmartlogDbContext(options);
        Db.Database.Migrate();

        var role = new Role { Id = Guid.NewGuid(), Nom = "Test", PermissionsJson = "{\"*\":true}" };
        Db.Roles.Add(role);
        Db.Utilisateurs.Add(new Utilisateur
        {
            Id = UtilisateurTestId,
            Identifiant = "utilisateur.test",
            MotDePasseHash = "(test)",
            Role = role
        });
        Db.SaveChanges();

        Journal = new JournalService(Db);
        Avenants = new AvenantService(Db, Journal);
        Attachements = new AttachementService(Db, Journal);
        Situations = new SituationService(Db, Journal, Attachements);
        Planning = new PlanificationService(Db, Journal);
        Courriers = new CourrierService(Db, Journal);
        Alertes = new AlerteService(Db, Journal);
        Recalcul = new RecalculCroiseService(
            Db, Avenants, Situations, Planning, Alertes, Journal);
        Projets = new ProjetService(Db, Journal, Avenants, Recalcul);
        Marches = new MarcheService(Db, Journal, Attachements);
        Ged = new GedService(Db, Journal, Ocr);
        Modeles = new ModeleDocumentService(Journal, Ged, Projets, Marches);
        Reserves = new ReserveService(Db, Journal, Modeles, Ged);
        AssistantIa = new AssistantIaService(
            Claude, ConfigurationAssistant, Projets, Ged, Situations,
            Planning, Alertes, Reserves, Courriers, Journal);
    }

    protected async Task<(ProjetDetail Projet, Marche Marche)> CreerProjetEtMarcheAsync(
        decimal montant = 1_250_000m,
        int delai = 300)
    {
        var projet = await Projets.CreerAsync(
            new ProjetCreation(
                "TEST-001", "Projet de test", "Lyon 3e", "Réhabilitation",
                montant, new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 2),
                delai, StatutProjet.EnCours),
            UtilisateurTestId);

        var marche = new Marche
        {
            ProjetId = projet.Id,
            ReferenceMarche = "MARCHE-TEST-001",
            DateSignature = new DateOnly(2026, 1, 28)
        };
        Db.Marches.Add(marche);
        await Db.SaveChangesAsync();

        return (projet, marche);
    }

    protected async Task<PrixUnitaire> CreerPrixUnitaireAsync(
        Guid marcheId, string reference, decimal prixUnitaireHT, decimal quantiteContractuelle)
    {
        var prix = new PrixUnitaire
        {
            MarcheId = marcheId,
            Reference = reference,
            Designation = $"Poste {reference}",
            Unite = "m²",
            PrixUnitaireHT = prixUnitaireHT,
            QuantiteContractuelle = quantiteContractuelle
        };
        Db.PrixUnitaires.Add(prix);
        await Db.SaveChangesAsync();
        return prix;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connexion.Dispose();
        GC.SuppressFinalize(this);
    }
}
