using Smartlog.Domain.Enums;

namespace Smartlog.Domain.Entities;

public class Reserve
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public int Numero { get; set; }
    public string Description { get; set; } = string.Empty;
    public PrioriteReserve Priorite { get; set; }

    public Guid? EntrepriseResponsableId { get; set; }
    public Intervenant? EntrepriseResponsable { get; set; }

    public DateOnly DateConstatation { get; set; }
    public int DelaiLeveeJours { get; set; }
    public StatutReserve Statut { get; set; } = StatutReserve.Nouvelle;
    public DateOnly? DateLevee { get; set; }
}

/// <summary>
/// Nommé "DocumentGed" (et non "Document") pour éviter toute confusion avec
/// les types système .NET/XML du même nom.
/// EntiteLiee* permet d'associer un document à n'importe quelle autre entité
/// (réserve, avenant, courrier...) sans multiplier les clés étrangères.
/// </summary>
public class DocumentGed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public string TypeDocument { get; set; } = string.Empty;
    public string CheminFichier { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string? ContenuOcr { get; set; }

    public string? EntiteLieeType { get; set; }
    public Guid? EntiteLieeId { get; set; }
}

public class Courrier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public SensCourrier Sens { get; set; }
    public string Expediteur { get; set; } = string.Empty;
    public string Destinataire { get; set; } = string.Empty;
    public string Objet { get; set; } = string.Empty;
    /// <summary>Date d'émission (sortant) ou de réception (entrant).</summary>
    public DateOnly DateCourrier { get; set; }
    public int? DelaiReponseJours { get; set; }
    public DateOnly? DateReponse { get; set; }
    public StatutCourrier Statut { get; set; } = StatutCourrier.EnAttente;
}

/// <summary>
/// EntiteSource* référence l'élément à l'origine de l'alerte (tâche en retard,
/// courrier sans réponse, garantie proche d'expiration...).
/// </summary>
public class Alerte
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjetId { get; set; }
    public Projet? Projet { get; set; }

    public string EntiteSourceType { get; set; } = string.Empty;
    public Guid EntiteSourceId { get; set; }

    public DateOnly DateEcheance { get; set; }
    public PrioriteAlerte Priorite { get; set; }
    public bool EstLue { get; set; }
}
