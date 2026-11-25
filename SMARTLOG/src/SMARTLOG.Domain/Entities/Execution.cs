using Smartlog.Domain.Enums;

namespace Smartlog.Domain.Entities;

public class Attachement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public DateOnly Date { get; set; }
    public StatutAttachement Statut { get; set; } = StatutAttachement.Brouillon;

    public ICollection<LigneAttachement> Lignes { get; set; } = new List<LigneAttachement>();
}

/// <summary>
/// MontantCalcule est TOUJOURS dérivé de QuantiteExecutee * PrixUnitaire.PrixUnitaireHT.
/// Il n'existe pas de saisie manuelle parallèle de ce montant.
/// </summary>
public class LigneAttachement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AttachementId { get; set; }
    public Attachement? Attachement { get; set; }

    public Guid PrixUnitaireId { get; set; }
    public PrixUnitaire? PrixUnitaire { get; set; }

    public decimal QuantiteExecutee { get; set; }
    public decimal MontantCalcule { get; set; }
}

public class Situation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    /// <summary>Format "AAAA-MM", ex. "2026-11".</summary>
    public string Periode { get; set; } = string.Empty;
    public TypeSituation Type { get; set; }
    public decimal MontantCumule { get; set; }
    public decimal RetenueGarantie { get; set; }
    public decimal Avance { get; set; }
    public decimal Penalites { get; set; }
    public StatutSituation Statut { get; set; } = StatutSituation.Brouillon;
}
