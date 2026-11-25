using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

public interface IAvenantService
{
    /// <summary>
    /// Initialise les valeurs dérivées d'un projet à partir de son montant
    /// initial et de son délai contractuel. Avec ValiderAsync et AnnulerAsync,
    /// c'est le seul point de l'application qui écrit MontantActualise et
    /// DelaiActualiseJours. Opération journalisée et idempotente.
    /// </summary>
    Task InitialiserProjetAsync(Guid projetId, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>Liste les avenants d'un marché, du plus ancien au plus récent.</summary>
    Task<IReadOnlyList<AvenantResume>> ListerAsync(Guid marcheId, CancellationToken ct = default);

    /// <summary>
    /// Recalcule les valeurs dérivées du projet (montant, délai et date
    /// prévisionnelle) sans créer ni modifier d'avenant. Point d'entrée unique
    /// pour les mutations qui changent une donnée contractuelle du projet
    /// (date de commencement notamment). Opération journalisée et idempotente.
    /// </summary>
    Task RecalculerValeursDeriveesAsync(
        Guid projetId, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>
    /// Calcule l'impact d'un avenant éventuel sans écrire quoi que ce soit :
    /// ni l'avenant, ni le projet, ni le journal. Utilisé par le bloc de
    /// simulation en temps réel de l'écran « Avenants ».
    /// </summary>
    Task<SimulationAvenant> SimulerAsync(
        Guid marcheId,
        decimal montantDelta,
        int delaiDeltaJours,
        CancellationToken ct = default);

    /// <summary>Crée un avenant en brouillon, sans effet sur le projet.</summary>
    Task<AvenantResume> CreerAsync(
        Guid marcheId,
        AvenantCreation donnees,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>
    /// Valide un avenant brouillon : recalcule MontantActualise et
    /// DelaiActualiseJours sur le Projet associé, journalise l'opération,
    /// et verrouille l'avenant (il ne redevient modifiable que par
    /// annulation explicite, elle aussi journalisée).
    /// </summary>
    Task<Projet> ValiderAsync(Guid avenantId, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>
    /// Annule un avenant déjà validé : recalcule le projet comme si
    /// l'avenant n'avait jamais existé. Opération explicite et journalisée,
    /// jamais silencieuse (§6 du cahier des charges).
    /// </summary>
    Task<Projet> AnnulerAsync(Guid avenantId, Guid utilisateurId, CancellationToken ct = default);
}

public sealed record AvenantCreation(
    int Numero,
    TypeAvenant Type,
    decimal MontantDelta,
    int DelaiDeltaJours);

public sealed record AvenantResume(
    Guid Id,
    Guid MarcheId,
    int Numero,
    TypeAvenant Type,
    string TypeLibelle,
    decimal MontantDelta,
    int DelaiDeltaJours,
    StatutAvenant Statut,
    string StatutLibelle,
    DateOnly? DateValidation);

/// <summary>Résultat d'une simulation : valeurs avant/après, sans aucune écriture.</summary>
public sealed record SimulationAvenant(
    decimal MontantActuel,
    decimal NouveauMontant,
    int DelaiActuelJours,
    int NouveauDelaiJours,
    DateOnly? DateFinActuelle,
    DateOnly? NouvelleDateFin);
