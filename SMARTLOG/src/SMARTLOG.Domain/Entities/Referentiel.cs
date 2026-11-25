using Smartlog.Domain.Enums;

namespace Smartlog.Domain.Entities;

/// <summary>
/// Entité pivot : un chantier BTP suivi de bout en bout dans SMARTLOG.
/// Les montants/délais "Actualise" sont des valeurs dérivées, recalculées
/// par les services métier (jamais saisies manuellement en parallèle).
/// </summary>
public class Projet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;

    public Guid? MaitreOuvrageId { get; set; }
    public Intervenant? MaitreOuvrage { get; set; }

    public Guid? MaitreOeuvreId { get; set; }
    public Intervenant? MaitreOeuvre { get; set; }

    public Guid? EntrepriseId { get; set; }
    public Intervenant? Entreprise { get; set; }

    public string? Localisation { get; set; }
    public string? NatureTravaux { get; set; }

    public decimal MontantInitial { get; set; }
    public decimal MontantActualise { get; set; }

    public DateOnly? DateNotification { get; set; }
    public DateOnly? DateCommencement { get; set; }
    public int DelaiContractuelJours { get; set; }
    public int DelaiActualiseJours { get; set; }
    public DateOnly? DatePrevisionnelleAchevement { get; set; }
    public DateOnly? DateReelleAchevement { get; set; }

    public StatutProjet Statut { get; set; } = StatutProjet.EnCours;

    public Marche? Marche { get; set; }
    public ICollection<Tache> Taches { get; set; } = new List<Tache>();
    public ICollection<Reserve> Reserves { get; set; } = new List<Reserve>();
    public ICollection<Situation> Situations { get; set; } = new List<Situation>();
    public ICollection<Attachement> Attachements { get; set; } = new List<Attachement>();
    public ICollection<DocumentGed> Documents { get; set; } = new List<DocumentGed>();
    public ICollection<Courrier> Courriers { get; set; } = new List<Courrier>();
    public ICollection<Alerte> Alertes { get; set; } = new List<Alerte>();
}

/// <summary>
/// Référentiel partagé entre projets (une entreprise ou un bureau de contrôle
/// intervient souvent sur plusieurs chantiers).
/// </summary>
public class Intervenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RaisonSociale { get; set; } = string.Empty;
    public TypeIntervenant Type { get; set; }
    public string? Coordonnees { get; set; }
}
