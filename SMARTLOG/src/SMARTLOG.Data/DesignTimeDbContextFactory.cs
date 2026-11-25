using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Smartlog.Data;

/// <summary>
/// Fabrique utilisée uniquement par les outils EF Core (dotnet ef) pour
/// construire le contexte au design-time. L'application, elle, construit
/// son contexte via l'injection de dépendances dans App.xaml.cs.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartlogDbContext>
{
    public SmartlogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartlogDbContext>()
            .UseSqlite("Data Source=smartlog-design.db")
            .Options;

        return new SmartlogDbContext(options);
    }
}
