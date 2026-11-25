namespace Smartlog.Services;

/// <summary>
/// Livre les modèles Excel de toutes les catégories métier. Les classeurs sont
/// générés en Open XML et déposés dans %LOCALAPPDATA%\Smartlog\Modeles ; un
/// modèle déjà présent n'est jamais écrasé.
/// </summary>
public sealed class ModeleExcelService : IModeleExcelService
{
    private readonly IReadOnlyList<JetonModele> _jetons;
    private readonly IReadOnlyList<ModeleExcel> _modeles;

    public ModeleExcelService(
        IModeleDocumentService modelesDocumentaires,
        string? dossierModeles = null)
    {
        _jetons = modelesDocumentaires.ListerJetonsDisponibles();
        _modeles = ConstruireModeles();
        DossierModeles = dossierModeles ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartlog", "Modeles");
    }

    public string DossierModeles { get; }

    public IReadOnlyList<ModeleExcel> ListerModeles() => _modeles;

    public async Task<IReadOnlyList<ModeleExcelFichier>> CreerModelesAsync(
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(DossierModeles);
        var resultat = new List<ModeleExcelFichier>(_modeles.Count);

        foreach (var modele in _modeles)
        {
            ct.ThrowIfCancellationRequested();
            var chemin = Path.Combine(DossierModeles, modele.NomFichier);
            var cree = !File.Exists(chemin);
            if (cree)
            {
                var octets = XlsxWriter.Generer(modele, _jetons);
                await File.WriteAllBytesAsync(chemin, octets, ct).ConfigureAwait(false);
            }
            resultat.Add(new ModeleExcelFichier(modele.NomFichier, chemin, cree));
        }

        return resultat;
    }

    private static IReadOnlyList<ModeleExcel> ConstruireModeles() => new[]
    {
        new ModeleExcel(
            "01_Fiche_Projet.xlsx", "Fiche projet", "Projets",
            new[]
            {
                new ColonneExcel("Code projet", "Identifiant unique, ex. PRJ-2026-001", 20),
                new ColonneExcel("Intitulé", "Nom du chantier", 38),
                new ColonneExcel("Localisation", "Ville / adresse du chantier", 26),
                new ColonneExcel("Nature des travaux", "Description libre des travaux", 38),
                new ColonneExcel("Maître d'ouvrage", "Raison sociale du maître d'ouvrage", 30),
                new ColonneExcel("Maître d'œuvre", "Raison sociale du maître d'œuvre", 30),
                new ColonneExcel("Entreprise", "Entreprise attributaire du marché", 30),
                new ColonneExcel("Montant initial (DA)", "Montant contractuel initial en dinars algériens", 22),
                new ColonneExcel("Montant actualisé (DA)", "Valeur calculée par SMARTLOG après avenants", 24),
                new ColonneExcel("Délai contractuel (j)", "Délai initial en jours calendaires", 22),
                new ColonneExcel("Délai actualisé (j)", "Valeur calculée par SMARTLOG après avenants", 22),
                new ColonneExcel("Date notification", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Date commencement", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Fin prévisionnelle", "Valeur calculée par SMARTLOG", 20),
                new ColonneExcel("Statut", "En cours / Terminé / Suspendu", 18)
            },
            new[]
            {
                "Une ligne par projet ; le code projet doit être unique.",
                "Les montants actualisés, délais actualisés et fins prévisionnelles sont calculés par SMARTLOG : ne les saisissez pas.",
                "Importez le classeur rempli dans le dossier du projet via Documents ▸ Importer des pièces."
            }),

        new ModeleExcel(
            "02_Contrat_Marche.xlsx", "Contrat / Marché", "Marché et contrats",
            new[]
            {
                new ColonneExcel("Référence marché", "Référence officielle du marché", 24),
                new ColonneExcel("Code projet", "Projet auquel le marché est rattaché", 20),
                new ColonneExcel("Date signature", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Montant contractuel (DA)", "Montant initial du contrat en DA", 24),
                new ColonneExcel("Total bordereau (DA)", "Somme calculée des prix unitaires", 24),
                new ColonneExcel("Délai contractuel (j)", "Délai global en jours", 22),
                new ColonneExcel("Date notification", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Date commencement", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Maître d'ouvrage", "Raison sociale", 28),
                new ColonneExcel("Entreprise", "Titulaire du marché", 28),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "Un seul marché par projet dans SMARTLOG.",
                "Les pièces du marché (PDF, Word, Excel) peuvent être importées dans le dossier « Marchés » de la GED.",
                "Le total du bordereau est calculé à partir des prix unitaires : ne le saisissez pas manuellement."
            }),

        new ModeleExcel(
            "03_Avenant.xlsx", "Avenant", "Avenants",
            new[]
            {
                new ColonneExcel("Numéro", "Numéro séquentiel de l'avenant", 14),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Référence marché", "Marché modifié", 24),
                new ColonneExcel("Type", "Financier / Délai / Mixte", 18),
                new ColonneExcel("Date validation", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Montant delta (DA)", "Écart positif ou négatif en DA", 22),
                new ColonneExcel("Délai delta (j)", "Écart de délai en jours", 18),
                new ColonneExcel("Montant avant (DA)", "Montant actualisé avant avenant", 22),
                new ColonneExcel("Montant après (DA)", "Valeur calculée par SMARTLOG", 22),
                new ColonneExcel("Justificatif", "Nom de la pièce justificative archivée", 30),
                new ColonneExcel("Observations", "Motif de l'avenant", 40)
            },
            new[]
            {
                "Un avenant en brouillon ne modifie ni le montant ni le délai du projet.",
                "Seule la validation d'un avenant déclenche le recalcul du montant actualisé, du délai actualisé et de la fin prévisionnelle.",
                "Le justificatif peut être importé dans le dossier « Avenants » de la GED."
            }),

        new ModeleExcel(
            "04_Attachement.xlsx", "Attachement", "Attachements",
            new[]
            {
                new ColonneExcel("Date", "Date de l'attachement (AAAA-MM-JJ)", 18),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Référence prix unitaire", "Référence du poste du bordereau", 26),
                new ColonneExcel("Désignation", "Libellé du poste", 34),
                new ColonneExcel("Quantité exécutée", "Quantité réalisée (décimale)", 22),
                new ColonneExcel("Prix unitaire HT (DA)", "Prix du bordereau en DA", 24),
                new ColonneExcel("Montant calculé (DA)", "Quantité × prix unitaire, calculé par SMARTLOG", 26),
                new ColonneExcel("Statut", "Brouillon / Contradictoire / Valide", 20),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "Le montant d'une ligne est toujours calculé : quantité exécutée × prix unitaire HT.",
                "Seuls les attachements validés alimentent les situations financières.",
                "Un dépassement de reliquat est signalé en rouge et doit être régularisé par un avenant."
            }),

        new ModeleExcel(
            "05_Situation.xlsx", "Situation financière", "Situations",
            new[]
            {
                new ColonneExcel("Période", "Format AAAA-MM", 16),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Type", "Provisoire / Définitive", 18),
                new ColonneExcel("Montant cumulé (DA)", "Cumul des attachements validés", 24),
                new ColonneExcel("Retenue garantie (DA)", "Retenue calculée par SMARTLOG (5 %)", 24),
                new ColonneExcel("Avance (DA)", "Avance calculée par SMARTLOG (10 %)", 22),
                new ColonneExcel("Pénalités (DA)", "Pénalités éventuelles", 20),
                new ColonneExcel("Net à payer (DA)", "Montant calculé par SMARTLOG", 24),
                new ColonneExcel("Statut", "Brouillon / Émise / Payée", 18),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "La situation est générée et recalculée par SMARTLOG à partir des attachements validés.",
                "Les taux historiques de retenue et d'avance sont préservés lors d'un recalcul croisé.",
                "Le dossier GED associé est « Situations »."
            }),

        new ModeleExcel(
            "06_Finances.xlsx", "Suivi financier", "Finances",
            new[]
            {
                new ColonneExcel("Période", "Format AAAA-MM", 16),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Montant contractuel (DA)", "Montant du marché", 26),
                new ColonneExcel("Engagé (DA)", "Cumul engagé à la période", 22),
                new ColonneExcel("Payé (DA)", "Cumul payé à la période", 22),
                new ColonneExcel("Restant dû (DA)", "Montant contractuel − payé", 22),
                new ColonneExcel("Situation liée", "Période de la situation correspondante", 20),
                new ColonneExcel("Commentaire", "Commentaire libre", 40)
            },
            new[]
            {
                "Les indicateurs financiers sont dérivés : SMARTLOG les calcule depuis les marchés, avenants et situations.",
                "Utilisez ce classeur comme pièce de suivi ou de reporting, puis archivez-le dans la GED du projet."
            }),

        new ModeleExcel(
            "07_Reserve.xlsx", "Réserve", "Réserves",
            new[]
            {
                new ColonneExcel("Numéro", "Numéro de la réserve", 14),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Description", "Description du défaut constaté", 42),
                new ColonneExcel("Priorité", "Basse / Moyenne / Haute", 16),
                new ColonneExcel("Date constatation", "Format AAAA-MM-JJ", 20),
                new ColonneExcel("Délai levée (j)", "Délai accordé en jours", 18),
                new ColonneExcel("Entreprise responsable", "Entreprise chargée de la levée", 28),
                new ColonneExcel("Statut", "Nouvelle / En cours / En vérification / Levée / Clôturée", 30),
                new ColonneExcel("Date levée", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("PV généré", "Oui / Non — le PV est généré par SMARTLOG", 18),
                new ColonneExcel("Observations", "Commentaire libre", 36)
            },
            new[]
            {
                "Le cycle de vie est strict : Nouvelle → En cours → En vérification → Levée → Clôturée.",
                "À la levée, SMARTLOG génère un procès-verbal et l'archive dans la GED (dossier « Réserves »).",
                "Une réserve non levée à échéance alimente le centre d'alertes."
            }),

        new ModeleExcel(
            "08_Proces_Verbal.xlsx", "Procès-verbal", "Procès-verbaux",
            new[]
            {
                new ColonneExcel("Type de PV", "Levée de réserve / Réunion / Contradictoire / Autre", 28),
                new ColonneExcel("Numéro", "Numéro du procès-verbal", 14),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Date", "Date de la séance (AAAA-MM-JJ)", 18),
                new ColonneExcel("Lieu", "Lieu de la réunion", 24),
                new ColonneExcel("Participants", "Noms et qualités des participants", 40),
                new ColonneExcel("Objet", "Objet de la séance", 36),
                new ColonneExcel("Décisions", "Décisions prises", 44),
                new ColonneExcel("Actions à suivre", "Actions et responsables", 44),
                new ColonneExcel("Échéance", "Date d'échéance des actions", 18),
                new ColonneExcel("Signataires", "Noms des signataires", 30)
            },
            new[]
            {
                "Un PV de levée de réserve est généré automatiquement par SMARTLOG lors du passage au statut « Levée ».",
                "Ce modèle couvre les autres PV (réunion de chantier, contradictoire, commission…).",
                "Archivez le PV rempli dans la GED ; la recherche plein texte exploitera son contenu si le format est PDF ou image avec OCR."
            }),

        new ModeleExcel(
            "09_Planning_Taches.xlsx", "Planning / Tâches", "Planning",
            new[]
            {
                new ColonneExcel("WBS", "Numéro d'arborescence, ex. 1.2.3", 12),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Libellé", "Libellé de la tâche", 38),
                new ColonneExcel("Tâche parent", "WBS de la tâche parent (vide si racine)", 22),
                new ColonneExcel("Date début prévue", "Format AAAA-MM-JJ", 20),
                new ColonneExcel("Date fin prévue", "Format AAAA-MM-JJ", 20),
                new ColonneExcel("Avancement (%)", "Pourcentage d'avancement saisi", 18),
                new ColonneExcel("Date début réelle", "Format AAAA-MM-JJ", 20),
                new ColonneExcel("Date fin réelle", "Format AAAA-MM-JJ", 20),
                new ColonneExcel("Chemin critique", "Calculé par SMARTLOG (CPM)", 20),
                new ColonneExcel("Observations", "Commentaire libre", 36)
            },
            new[]
            {
                "Les dates prévues et le chemin critique sont calculés par le moteur CPM de SMARTLOG.",
                "Une tâche en retard alimente automatiquement le centre d'alertes.",
                "Les liaisons (Fin–Début, Début–Début, Fin–Fin, Début–Fin) sont gérées dans le module Planning."
            }),

        new ModeleExcel(
            "10_Courrier.xlsx", "Courrier", "Courrier",
            new[]
            {
                new ColonneExcel("Sens", "Entrant / Sortant", 14),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Expéditeur", "Nom de l'expéditeur", 28),
                new ColonneExcel("Destinataire", "Nom du destinataire", 28),
                new ColonneExcel("Objet", "Objet du courrier", 40),
                new ColonneExcel("Date courrier", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Délai réponse (j)", "Délai de réponse en jours (facultatif)", 20),
                new ColonneExcel("Date réponse", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Statut", "En attente / Répondu / Sans suite", 20),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "L'échéance de réponse est calculée par SMARTLOG à partir de la date du courrier et du délai.",
                "Un courrier sans réponse à échéance alimente le centre d'alertes.",
                "Le dossier GED associé est « Courrier »."
            }),

        new ModeleExcel(
            "11_Alertes.xlsx", "Alertes", "Centre d'alertes",
            new[]
            {
                new ColonneExcel("Source", "Projet / Tâche / Réserve / Courrier", 20),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Référence source", "Identifiant de l'élément à l'origine de l'alerte", 26),
                new ColonneExcel("Échéance", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Priorité", "Basse / Moyenne / Haute", 16),
                new ColonneExcel("Action corrective", "Action prévue", 42),
                new ColonneExcel("Statut lecture", "Lue / Non lue", 18),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "Les alertes sont générées par les quatre règles de SMARTLOG : échéance projet, tâche en retard, réserve non levée, courrier sans réponse.",
                "Une seconde analyse ne duplique pas les alertes existantes.",
                "Utilisez ce classeur pour le suivi humain des actions correctives."
            }),

        new ModeleExcel(
            "12_Bordereau_Prix_Unitaires.xlsx", "Bordereau de prix unitaires", "Marché et contrats",
            new[]
            {
                new ColonneExcel("Référence", "Référence du poste, ex. PU-01", 16),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Désignation", "Libellé du poste", 40),
                new ColonneExcel("Unité", "m², m³, ml, kg, forfait…", 14),
                new ColonneExcel("Prix unitaire HT (DA)", "Prix en dinars algériens", 24),
                new ColonneExcel("Quantité contractuelle", "Quantité du marché", 22),
                new ColonneExcel("Montant total (DA)", "Prix × quantité, calculé par SMARTLOG", 24),
                new ColonneExcel("Observations", "Commentaire libre", 36)
            },
            new[]
            {
                "Le total du bordereau est la somme des prix unitaires × quantités contractuelles.",
                "Modifier un prix recalcule immédiatement toutes les lignes d'attachement qui l'utilisent.",
                "Le dossier GED associé est « Marchés »."
            }),

        new ModeleExcel(
            "13_Document_GED.xlsx", "Document GED générique", "Documents",
            new[]
            {
                new ColonneExcel("Type document", "Contrat, facture, plan, note, image…", 24),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Catégorie", "Excel / PDF / Word / Image", 18),
                new ColonneExcel("Dossier", "Marchés / Avenants / Réserves / Situations / Courrier / Divers", 30),
                new ColonneExcel("Date document", "Format AAAA-MM-JJ", 18),
                new ColonneExcel("Référence", "Référence interne du document", 22),
                new ColonneExcel("Version", "Numéro de version attribué par la GED", 14),
                new ColonneExcel("Mots-clés", "Mots-clés pour la recherche plein texte", 40),
                new ColonneExcel("Observations", "Commentaire libre", 40)
            },
            new[]
            {
                "Ce modèle couvre les documents qui n'ont pas de formulaire dédié.",
                "La GED versionne automatiquement les fichiers importés (1_nom, 2_nom…).",
                "L'OCR est tenté pour les PDF et images ; sans Tesseract, le document est archivé avec le statut « sans OCR »."
            }),

        new ModeleExcel(
            "14_Rapport_Activite.xlsx", "Rapport d'activité", "Reporting",
            new[]
            {
                new ColonneExcel("Période", "Format AAAA-MM", 16),
                new ColonneExcel("Code projet", "Projet concerné", 20),
                new ColonneExcel("Avancement global (%)", "Avancement moyen du planning", 22),
                new ColonneExcel("Montant engagé (DA)", "Cumul engagé en DA", 22),
                new ColonneExcel("Montant payé (DA)", "Cumul payé en DA", 22),
                new ColonneExcel("Tâches en retard", "Nombre de tâches en retard", 20),
                new ColonneExcel("Réserves ouvertes", "Réserves non levées", 20),
                new ColonneExcel("Alertes actives", "Alertes non lues", 18),
                new ColonneExcel("Commentaire", "Commentaire libre", 44)
            },
            new[]
            {
                "Les indicateurs sont consolidés à partir des modules Finances, Planning, Réserves et Alertes.",
                "Ce rapport peut être archivé dans la GED puis partagé avec le maître d'ouvrage.",
                "Les montants sont exprimés en DA (dinar algérien)."
            })
    };
}
