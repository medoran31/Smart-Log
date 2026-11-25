namespace Smartlog.Services;

public enum FormatDocument
{
    Word,
    Pdf
}

/// <summary>Demande de génération à partir d'un modèle textuel à jetons.</summary>
public sealed record ModeleDocumentDemande(
    Guid ProjetId,
    string ContenuModele,
    string Titre,
    FormatDocument Format,
    Guid UtilisateurId,
    IReadOnlyDictionary<string, string>? ValeursSupplementaires = null,
    string? EntiteLieeType = null,
    Guid? EntiteLieeId = null);

public sealed record DocumentGenere(
    string CheminFichier,
    string NomFichier,
    FormatDocument Format,
    string ContenuGenere,
    IReadOnlyList<string> JetonsNonResolus);

public sealed record JetonModele(string Cle, string Description);

/// <summary>
/// Moteur transversal de modèles documentaires. Il alimente les jetons
/// {{Projet.*}} et {{Marche.*}} à partir des services métier, accepte des
/// valeurs supplémentaires (réserve, courrier, situation...) et produit un
/// fichier Word (.docx) ou PDF enregistré dans la GED du projet.
/// </summary>
public interface IModeleDocumentService
{
    IReadOnlyList<JetonModele> ListerJetonsDisponibles();

    Task<DocumentGenere> GenererAsync(
        ModeleDocumentDemande demande,
        CancellationToken ct = default);

    Task<DocumentGenere> GenererDepuisFichierAsync(
        Guid projetId,
        string cheminModele,
        string titre,
        FormatDocument format,
        Guid utilisateurId,
        IReadOnlyDictionary<string, string>? valeursSupplementaires = null,
        CancellationToken ct = default);
}
