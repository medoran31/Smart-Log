using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smartlog.App.ViewModels;
using Smartlog.App.Views;
using Smartlog.Data;
using Smartlog.Services;
using Smartlog.Services.Ged;

namespace Smartlog.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dossierDonnees = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartlog");
        Directory.CreateDirectory(dossierDonnees);
        var cheminBase = Path.Combine(dossierDonnees, "smartlog.db");

        // Une restauration programmée s'applique avant toute ouverture EF Core.
        SauvegardeService.AppliquerRestaurationEnAttente(cheminBase);

        var services = new ServiceCollection();

        services.AddDbContext<SmartlogDbContext>(options =>
            options.UseSqlite($"Data Source={cheminBase}"));

        services.AddScoped<IJournalService, JournalService>();
        services.AddScoped<IAvenantService, AvenantService>();
        services.AddScoped<ISituationService, SituationService>();
        services.AddScoped<IDonneesDemonstrationService, DonneesDemonstrationService>();
        services.AddScoped<IProjetService, ProjetService>();
        services.AddScoped<IMarcheService, MarcheService>();
        services.AddScoped<IAttachementService, AttachementService>();
        services.AddScoped<IPlanificationService, PlanificationService>();
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddScoped<IGedService, GedService>();
        services.AddScoped<IModeleDocumentService, ModeleDocumentService>();
        services.AddSingleton<IModeleExcelService, ModeleExcelService>();
        services.AddScoped<IReserveService, ReserveService>();
        services.AddScoped<ICourrierService, CourrierService>();
        services.AddScoped<IAlerteService, AlerteService>();
        services.AddScoped<IRecalculCroiseService, RecalculCroiseService>();
        services.AddSingleton<IAssistantIaConfiguration, ConfigurationAssistantIa>();
        services.AddSingleton<IClientClaude, ClientClaude>();
        services.AddScoped<IAssistantIaService, AssistantIaService>();
        services.AddSingleton<ISauvegardeService>(new SauvegardeService(cheminBase));

        services.AddSingleton<ProjetsViewModel>();
        services.AddSingleton<TableauDeBoardViewModel>();
        services.AddSingleton<FicheProjetViewModel>();
        services.AddSingleton<MarchesViewModel>();
        services.AddSingleton<AvenantsViewModel>();
        services.AddSingleton<GedViewModel>();
        services.AddSingleton<AttachementsViewModel>();
        services.AddSingleton<FinancesViewModel>();
        services.AddSingleton<PlanningViewModel>();
        services.AddSingleton<ReservesViewModel>();
        services.AddSingleton<CourriersViewModel>();
        services.AddSingleton<AlertesViewModel>();
        services.AddSingleton<AssistantIaViewModel>();
        services.AddSingleton<SauvegardeViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();

        // Applique les migrations en attente à chaque démarrage : c'est le
        // mécanisme de mise à jour du schéma demandé au §9 du cahier des charges.
        using (var scope = Services.CreateScope())
        {
            var fournisseur = scope.ServiceProvider;
            fournisseur.GetRequiredService<SmartlogDbContext>().Database.Migrate();

            // Premier lancement : seeder + calculs métier + traçabilité.
            fournisseur.GetRequiredService<IDonneesDemonstrationService>()
                .InitialiserAsync()
                .GetAwaiter()
                .GetResult();
        }

        Services.GetRequiredService<MainWindow>().Show();
    }
}
