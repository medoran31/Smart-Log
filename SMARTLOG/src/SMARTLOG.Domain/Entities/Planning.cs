namespace Smartlog.Domain.Entities;

public class Tache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public Guid? ParentTacheId { get; set; }
    public Tache? ParentTache { get; set; }
    public ICollection<Tache> SousTaches { get; set; } = new List<Tache>();

    public string Libelle { get; set; } = string.Empty;
    public DateOnly DateDebutPrevue { get; set; }
    public DateOnly DateFinPrevue { get; set; }
    public DateOnly? DateDebutReelle { get; set; }
    public DateOnly? DateFinReelle { get; set; }
    public decimal AvancementPct { get; set; }

    /// <summary>Calculé par le moteur de planification, jamais saisi.</summary>
    public bool EstCheminCritique { get; set; }
}

public enum TypeLiaison
{
    FinDebut,
    DebutDebut,
    FinFin,
    DebutFin
}

public class LiaisonTache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TachePredecesseurId { get; set; }
    public Tache? TachePredecesseur { get; set; }
    public Guid TacheSuccesseurId { get; set; }
    public Tache? TacheSuccesseur { get; set; }
    public TypeLiaison TypeLiaison { get; set; }
}
