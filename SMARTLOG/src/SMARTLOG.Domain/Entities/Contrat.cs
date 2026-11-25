using Smartlog.Domain.Enums;

namespace Smartlog.Domain.Entities;

public class Marche
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public string ReferenceMarche { get; set; } = string.Empty;
    public DateOnly? DateSignature { get; set; }

    public ICollection<Avenant> Avenants { get; set; } = new List<Avenant>();
    public ICollection<PrixUnitaire> PrixUnitaires { get; set; } = new List<PrixUnitaire>();
}

/// <summary>
/// Un avenant validé est le SEUL déclencheur du recalcul du montant et du
/// délai du marché (voir AvenantService). Tant qu'il est en brouillon,
/// il n'a aucun effet sur le reste de l'application.
/// </summary>
public class Avenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MarcheId { get; set; }
    public Marche? Marche { get; set; }

    public int Numero { get; set; }
    public TypeAvenant Type { get; set; }
    public decimal MontantDelta { get; set; }
    public int DelaiDeltaJours { get; set; }
    public StatutAvenant Statut { get; set; } = StatutAvenant.Brouillon;
    public DateOnly? DateValidation { get; set; }

    public Guid? JustificatifDocumentId { get; set; }
    public DocumentGed? JustificatifDocument { get; set; }
}

public class PrixUnitaire
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MarcheId { get; set; }
    public Marche? Marche { get; set; }

    public string Reference { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Unite { get; set; } = string.Empty;
    public decimal PrixUnitaireHT { get; set; }
    public decimal QuantiteContractuelle { get; set; }

    public ICollection<LigneAttachement> LignesAttachement { get; set; } = new List<LigneAttachement>();
}
