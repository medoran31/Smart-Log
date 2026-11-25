using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;

namespace Smartlog.Data;

public class SmartlogDbContext : DbContext
{
    public SmartlogDbContext(DbContextOptions<SmartlogDbContext> options) : base(options) { }

    public DbSet<Projet> Projets => Set<Projet>();
    public DbSet<Intervenant> Intervenants => Set<Intervenant>();
    public DbSet<Marche> Marches => Set<Marche>();
    public DbSet<Avenant> Avenants => Set<Avenant>();
    public DbSet<PrixUnitaire> PrixUnitaires => Set<PrixUnitaire>();
    public DbSet<Attachement> Attachements => Set<Attachement>();
    public DbSet<LigneAttachement> LignesAttachement => Set<LigneAttachement>();
    public DbSet<Situation> Situations => Set<Situation>();
    public DbSet<Tache> Taches => Set<Tache>();
    public DbSet<LiaisonTache> LiaisonsTache => Set<LiaisonTache>();
    public DbSet<Reserve> Reserves => Set<Reserve>();
    public DbSet<DocumentGed> Documents => Set<DocumentGed>();
    public DbSet<Courrier> Courriers => Set<Courrier>();
    public DbSet<Alerte> Alertes => Set<Alerte>();
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<JournalAction> JournalActions => Set<JournalAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Projet>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.MontantInitial).HasPrecision(18, 2);
            e.Property(p => p.MontantActualise).HasPrecision(18, 2);
            e.HasOne(p => p.Marche).WithOne(m => m.Projet!).HasForeignKey<Marche>(m => m.ProjetId);
        });

        modelBuilder.Entity<Avenant>(e =>
        {
            e.Property(a => a.MontantDelta).HasPrecision(18, 2);
            e.HasOne(a => a.Marche).WithMany(m => m.Avenants).HasForeignKey(a => a.MarcheId);
        });

        modelBuilder.Entity<PrixUnitaire>(e =>
        {
            e.Property(p => p.PrixUnitaireHT).HasPrecision(18, 2);
            e.Property(p => p.QuantiteContractuelle).HasPrecision(18, 3);
            e.HasOne(p => p.Marche).WithMany(m => m.PrixUnitaires).HasForeignKey(p => p.MarcheId);
        });

        modelBuilder.Entity<LigneAttachement>(e =>
        {
            e.Property(l => l.QuantiteExecutee).HasPrecision(18, 3);
            e.Property(l => l.MontantCalcule).HasPrecision(18, 2);
            e.HasOne(l => l.Attachement).WithMany(a => a.Lignes).HasForeignKey(l => l.AttachementId);
            e.HasOne(l => l.PrixUnitaire).WithMany(p => p.LignesAttachement).HasForeignKey(l => l.PrixUnitaireId);
        });

        modelBuilder.Entity<Situation>(e =>
        {
            e.Property(s => s.MontantCumule).HasPrecision(18, 2);
            e.Property(s => s.RetenueGarantie).HasPrecision(18, 2);
            e.Property(s => s.Avance).HasPrecision(18, 2);
            e.Property(s => s.Penalites).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Tache>(e =>
        {
            e.HasOne(t => t.ParentTache).WithMany(t => t.SousTaches)
                .HasForeignKey(t => t.ParentTacheId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiaisonTache>(e =>
        {
            e.HasOne(l => l.TachePredecesseur).WithMany()
                .HasForeignKey(l => l.TachePredecesseurId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.TacheSuccesseur).WithMany()
                .HasForeignKey(l => l.TacheSuccesseurId).OnDelete(DeleteBehavior.Restrict);
        });

        // Toutes les entités rattachées à un Projet partagent la même règle :
        // suppression du projet interdite tant que des enfants existent
        // (on archive un projet, on ne le supprime jamais physiquement).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var fk in entityType.GetForeignKeys())
            {
                if (fk.PrincipalEntityType.ClrType == typeof(Projet))
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
