namespace Smartlog.Services;

/// <summary>
/// Configuration locale de l'assistant IA. La clé API n'est jamais écrite
/// dans le code : elle est lue dans un fichier local ou dans l'environnement.
/// </summary>
public interface IAssistantIaConfiguration
{
    /// <summary>Clé API Claude, ou null si l'assistant n'est pas configuré.</summary>
    string? CleApi { get; }

    /// <summary>Identifiant du modèle Claude à interroger.</summary>
    string Modele { get; }

    /// <summary>Version de l'API Claude envoyée dans l'en-tête dédié.</summary>
    string VersionApi { get; }

    /// <summary>Identifiant d'espace de travail, si la clé n'y est pas rattachée.</summary>
    string? IdentifiantEspaceTravail { get; }

    /// <summary>Chemin du fichier de configuration affiché à l'utilisateur.</summary>
    string CheminFichier { get; }

    bool EstConfigure { get; }

    /// <summary>Description sans secret, destinée à l'interface.</summary>
    string Resumer();
}
