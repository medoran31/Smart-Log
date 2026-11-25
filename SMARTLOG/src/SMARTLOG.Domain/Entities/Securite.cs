namespace Smartlog.Domain.Entities;

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nom { get; set; } = string.Empty;
    /// <summary>Permissions sérialisées en JSON (lecture/écriture par module).</summary>
    public string PermissionsJson { get; set; } = "{}";
}

public class Utilisateur
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identifiant { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

/// <summary>
/// Trace CHAQUE création/modification/suppression dans l'application.
/// ValeurAvant/ValeurApres sont sérialisés en JSON pour rester génériques
/// quelle que soit l'entité concernée.
/// </summary>
public class JournalAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UtilisateurId { get; set; }
    public Utilisateur? Utilisateur { get; set; }

    public string EntiteType { get; set; } = string.Empty;
    public Guid EntiteId { get; set; }
    public string Action { get; set; } = string.Empty; // Creation / Modification / Suppression
    public string? ValeurAvantJson { get; set; }
    public string? ValeurApresJson { get; set; }
    public DateTime DateAction { get; set; } = DateTime.UtcNow;
}
