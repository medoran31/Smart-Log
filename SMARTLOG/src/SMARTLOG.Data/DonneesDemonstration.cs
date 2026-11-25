using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Data;

/// <summary>
/// Résultat de l'initialisation de démonstration. Les identifiants retournés
/// permettent au service d'orchestration d'appliquer ensuite les règles
/// métier (valeurs dérivées et traçabilité) sans jamais les recalculer ici.
/// </summary>
public sealed record ResultatDemonstration(
    bool DonneesCreees,
    Guid UtilisateurId,
    IReadOnlyList<Guid> ProjetIds,
    IReadOnlyList<Guid> AvenantIds,
    IReadOnlyList<Guid> SituationIds);

/// <summary>
/// Jeu de données de démonstration inséré uniquement lors du premier
/// lancement (base vide). Cette classe ne calcule aucune valeur dérivée :
/// montant actualisé, délai actualisé, cumul et avancement sont laissés à
/// leur valeur par défaut et seront initialisés par les services dédiés de
/// SMARTLOG.Services.
/// </summary>
public static class DonneesDemonstration
{
    public static readonly Guid UtilisateurDemonstrationId =
        Guid.Parse("11111111-1111-4111-8111-111111111111");

    public static async Task<ResultatDemonstration> InitialiserAsync(
        SmartlogDbContext db,
        CancellationToken ct = default)
    {
        if (await db.Projets.AnyAsync(ct))
        {
            return new ResultatDemonstration(
                false, Guid.Empty,
                Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<Guid>());
        }

        var role = new Role
        {
            Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Nom = "Administrateur démonstration",
            PermissionsJson = "{\"*\":true}"
        };
        db.Roles.Add(role);
        db.Utilisateurs.Add(new Utilisateur
        {
            Id = UtilisateurDemonstrationId,
            Identifiant = "admin.demo",
            MotDePasseHash = "(compte de démonstration)",
            RoleId = role.Id,
            Role = role
        });

        var projetIds = new List<Guid>();
        var avenantIds = new List<Guid>();
        var situationIds = new List<Guid>();

        // ---------- Projet 1 : réhabilitation d'un groupe scolaire ----------
        var projet1 = new Projet
        {
            Code = "PRJ-2026-001",
            Intitule = "Réhabilitation du groupe scolaire Les Tilleuls",
            MaitreOuvrage = new Intervenant
            {
                RaisonSociale = "Ville de Lyon",
                Type = TypeIntervenant.MaitreOuvrage,
                Coordonnees = "Direction du patrimoine — Lyon 3e"
            },
            MaitreOeuvre = new Intervenant
            {
                RaisonSociale = "Cabinet Arquetype",
                Type = TypeIntervenant.MaitreOeuvre,
                Coordonnees = "12 rue Victor Hugo, 69003 Lyon"
            },
            Entreprise = new Intervenant
            {
                RaisonSociale = "BTP Rhône-Alpes",
                Type = TypeIntervenant.Entreprise,
                Coordonnees = "Zone industrielle de Feyzin"
            },
            Localisation = "Lyon 3e (Rhône)",
            NatureTravaux = "Réhabilitation lourde, mise en accessibilité et performance énergétique",
            MontantInitial = 1_250_000m,
            DateNotification = new DateOnly(2026, 1, 15),
            DateCommencement = new DateOnly(2026, 2, 2),
            DelaiContractuelJours = 300,
            Statut = StatutProjet.EnCours
        };

        var marche1 = new Marche
        {
            ReferenceMarche = "MARCHE-2026-001",
            DateSignature = new DateOnly(2026, 1, 28),
            Projet = projet1
        };

        marche1.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-01",
            Designation = "Béton armé B30",
            Unite = "m³",
            PrixUnitaireHT = 185.00m,
            QuantiteContractuelle = 450m,
            Marche = marche1
        });
        marche1.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-02",
            Designation = "Carrelage grand format",
            Unite = "m²",
            PrixUnitaireHT = 62.50m,
            QuantiteContractuelle = 1_200m,
            Marche = marche1
        });
        marche1.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-03",
            Designation = "Menuiserie aluminium double vitrage",
            Unite = "m²",
            PrixUnitaireHT = 410.00m,
            QuantiteContractuelle = 180m,
            Marche = marche1
        });

        var avenant1 = new Avenant
        {
            Numero = 1,
            Type = TypeAvenant.Mixte,
            MontantDelta = 85_000m,
            DelaiDeltaJours = 21,
            Statut = StatutAvenant.Brouillon,
            Marche = marche1
        };

        var situation1 = new Situation
        {
            Periode = "2026-10",
            Type = TypeSituation.Provisoire,
            Statut = StatutSituation.Brouillon,
            Projet = projet1
        };

        projet1.Reserves.Add(new Reserve
        {
            Numero = 1,
            Description = "Fuite sur le réseau d'eau sanitaire au premier étage",
            Priorite = PrioriteReserve.Haute,
            DateConstatation = new DateOnly(2026, 10, 6),
            DelaiLeveeJours = 15,
            Statut = StatutReserve.EnCours,
            EntrepriseResponsable = projet1.Entreprise
        });
        projet1.Reserves.Add(new Reserve
        {
            Numero = 2,
            Description = "Écart de teinte constaté sur la façade sud",
            Priorite = PrioriteReserve.Moyenne,
            DateConstatation = new DateOnly(2026, 10, 14),
            DelaiLeveeJours = 30,
            Statut = StatutReserve.Nouvelle,
            EntrepriseResponsable = projet1.Entreprise
        });

        projet1.Courriers.Add(new Courrier
        {
            Sens = SensCourrier.Entrant,
            Expediteur = "Ville de Lyon — service urbanisme",
            Destinataire = "Maîtrise d'œuvre",
            Objet = "Observations sur le plan de raccordement",
            DateCourrier = new DateOnly(2026, 10, 20),
            DelaiReponseJours = 15
        });
        projet1.Courriers.Add(new Courrier
        {
            Sens = SensCourrier.Sortant,
            Expediteur = "Maîtrise d'œuvre",
            Destinataire = "Entreprise attributaire",
            Objet = "Transmission du plan de synthèse",
            DateCourrier = new DateOnly(2026, 11, 2),
            DelaiReponseJours = 30,
            Statut = StatutCourrier.Repondu,
            DateReponse = new DateOnly(2026, 11, 10)
        });

        db.Projets.Add(projet1);
        db.Marches.Add(marche1);
        db.Avenants.Add(avenant1);
        db.Situations.Add(situation1);

        // ---------- Projet 2 : poste de transformation HTA ----------
        var projet2 = new Projet
        {
            Code = "PRJ-2026-002",
            Intitule = "Construction d'un poste de transformation HTA",
            MaitreOuvrage = new Intervenant
            {
                RaisonSociale = "Syndicat d'électrification du Rhône",
                Type = TypeIntervenant.MaitreOuvrage,
                Coordonnees = "Siège de Villefranche-sur-Saône"
            },
            MaitreOeuvre = new Intervenant
            {
                RaisonSociale = "Ingénierie 69",
                Type = TypeIntervenant.MaitreOeuvre,
                Coordonnees = "8 avenue de la Liberté, 69400 Villefranche"
            },
            Entreprise = new Intervenant
            {
                RaisonSociale = "Électra Construction",
                Type = TypeIntervenant.Entreprise,
                Coordonnees = "Atelier de Montluel"
            },
            Localisation = "Villefranche-sur-Saône (Rhône)",
            NatureTravaux = "Génie civil, pose de câbles HTA et raccordement",
            MontantInitial = 780_000m,
            DateNotification = new DateOnly(2026, 3, 10),
            DateCommencement = new DateOnly(2026, 4, 6),
            DelaiContractuelJours = 210,
            Statut = StatutProjet.EnCours
        };

        var marche2 = new Marche
        {
            ReferenceMarche = "MARCHE-2026-002",
            DateSignature = new DateOnly(2026, 3, 24),
            Projet = projet2
        };

        marche2.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-01",
            Designation = "Terrassement et remblaiement",
            Unite = "m³",
            PrixUnitaireHT = 48.00m,
            QuantiteContractuelle = 3_200m,
            Marche = marche2
        });
        marche2.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-02",
            Designation = "Câble HTA 20 kV",
            Unite = "ml",
            PrixUnitaireHT = 32.00m,
            QuantiteContractuelle = 5_400m,
            Marche = marche2
        });
        marche2.PrixUnitaires.Add(new PrixUnitaire
        {
            Reference = "PU-03",
            Designation = "Poste préfabriqué HTA/BT",
            Unite = "unité",
            PrixUnitaireHT = 95_000.00m,
            QuantiteContractuelle = 1m,
            Marche = marche2
        });

        var avenant2 = new Avenant
        {
            Numero = 1,
            Type = TypeAvenant.Financier,
            MontantDelta = -12_500m,
            DelaiDeltaJours = 0,
            Statut = StatutAvenant.Brouillon,
            Marche = marche2
        };

        var situation2 = new Situation
        {
            Periode = "2026-11",
            Type = TypeSituation.Provisoire,
            Statut = StatutSituation.Brouillon,
            Projet = projet2
        };

        projet2.Reserves.Add(new Reserve
        {
            Numero = 1,
            Description = "Garde-corps de la plateforme non conforme au plan",
            Priorite = PrioriteReserve.Haute,
            DateConstatation = new DateOnly(2026, 11, 3),
            DelaiLeveeJours = 10,
            Statut = StatutReserve.Nouvelle,
            EntrepriseResponsable = projet2.Entreprise
        });
        projet2.Reserves.Add(new Reserve
        {
            Numero = 2,
            Description = "Retard de livraison du transformateur",
            Priorite = PrioriteReserve.Moyenne,
            DateConstatation = new DateOnly(2026, 11, 12),
            DelaiLeveeJours = 21,
            Statut = StatutReserve.EnCours,
            EntrepriseResponsable = projet2.Entreprise
        });

        projet2.Courriers.Add(new Courrier
        {
            Sens = SensCourrier.Sortant,
            Expediteur = "Maîtrise d'œuvre",
            Destinataire = "Électra Construction",
            Objet = "Mise en demeure — garde-corps non conformes",
            DateCourrier = new DateOnly(2026, 11, 5),
            DelaiReponseJours = 10
        });

        db.Projets.Add(projet2);
        db.Marches.Add(marche2);
        db.Avenants.Add(avenant2);
        db.Situations.Add(situation2);

        await db.SaveChangesAsync(ct);

        projetIds.Add(projet1.Id);
        projetIds.Add(projet2.Id);
        avenantIds.Add(avenant1.Id);
        avenantIds.Add(avenant2.Id);
        situationIds.Add(situation1.Id);
        situationIds.Add(situation2.Id);

        return new ResultatDemonstration(
            true, UtilisateurDemonstrationId, projetIds, avenantIds, situationIds);
    }
}
