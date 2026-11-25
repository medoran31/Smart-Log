using Smartlog.Domain.Entities;

namespace Smartlog.Services;

/// <summary>Donnée d'entrée du moteur CPM (aucune écriture, aucun DbContext).</summary>
internal sealed record TacheCpm(
    Guid Id,
    int DureeJours,
    int DebutSaisi,
    IReadOnlyList<LienCpm> Predecesseurs,
    IReadOnlyList<LienCpm> Successeurs);

internal sealed record LienCpm(Guid PredecesseurId, Guid SuccesseurId, TypeLiaison TypeLiaison);

/// <summary>Résultat du CPM : dates en DayNumber, marge et chemin critique.</summary>
internal sealed class ResultatCpm
{
    public Dictionary<Guid, int> Debuts { get; } = new();
    public Dictionary<Guid, int> Fins { get; } = new();
    public Dictionary<Guid, int> Marges { get; } = new();
    public HashSet<Guid> Critiques { get; } = new();
    public int DebutProjet { get; set; }
    public int FinProjet { get; set; }
}

/// <summary>
/// Algorithme CPM standard : tri topologique, passage avant (dates au plus
/// tôt), passage arrière (dates au plus tard), marge totale et chemin
/// critique. Les quatre types de liaisons sont pris en charge.
/// Les dates sont des DayNumber et les durées des jours calendaires inclusifs.
/// </summary>
internal static class CpmCalculateur
{
    public static ResultatCpm Calculer(
        IReadOnlyList<TacheCpm> taches,
        IReadOnlyDictionary<Guid, int> datesImposees)
    {
        var resultat = new ResultatCpm();
        if (taches.Count == 0)
            return resultat;

        var parId = taches.ToDictionary(t => t.Id);

        // Tri topologique de Kahn : détecte également les cycles de liaisons.
        var degres = taches.ToDictionary(t => t.Id, t => t.Predecesseurs.Count);
        var file = new Queue<Guid>(taches.Where(t => t.Predecesseurs.Count == 0).Select(t => t.Id));
        var ordre = new List<Guid>(taches.Count);
        while (file.Count > 0)
        {
            var id = file.Dequeue();
            ordre.Add(id);
            foreach (var successeur in parId[id].Successeurs.Select(l => l.SuccesseurId).Distinct())
            {
                if (--degres[successeur] == 0)
                    file.Enqueue(successeur);
            }
        }

        if (ordre.Count != taches.Count)
            throw new InvalidOperationException(
                "Cycle détecté dans les liaisons du planning : le chemin critique ne peut pas être calculé.");

        // Passage avant : date au plus tôt = max des contraintes entrantes.
        foreach (var id in ordre)
        {
            var tache = parId[id];
            var debut = datesImposees.TryGetValue(id, out var imposee)
                ? imposee
                : tache.Predecesseurs.Count == 0
                    ? tache.DebutSaisi
                    : int.MinValue;

            foreach (var lien in tache.Predecesseurs)
            {
                debut = Math.Max(debut, DebutMinimum(
                    lien.TypeLiaison,
                    resultat.Debuts[lien.PredecesseurId],
                    resultat.Fins[lien.PredecesseurId],
                    tache.DureeJours));
            }

            if (debut == int.MinValue)
                debut = tache.DebutSaisi;

            resultat.Debuts[id] = debut;
            resultat.Fins[id] = debut + tache.DureeJours - 1;
        }

        resultat.DebutProjet = resultat.Debuts.Values.Min();
        var finProjet = resultat.Fins.Values.Max();

        // Passage arrière : date au plus tard = min des contraintes sortantes.
        var debutsTard = new Dictionary<Guid, int>();
        var finsTard = new Dictionary<Guid, int>();
        for (var i = ordre.Count - 1; i >= 0; i--)
        {
            var id = ordre[i];
            var tache = parId[id];
            var finTard = tache.Successeurs.Count == 0 ? finProjet : int.MaxValue;

            foreach (var lien in tache.Successeurs)
            {
                finTard = Math.Min(finTard, FinMaximum(
                    lien.TypeLiaison,
                    debutsTard[lien.SuccesseurId],
                    finsTard[lien.SuccesseurId],
                    tache.DureeJours));
            }

            finsTard[id] = finTard;
            debutsTard[id] = finTard - tache.DureeJours + 1;

            var marge = debutsTard[id] - resultat.Debuts[id];
            resultat.Marges[id] = marge;
            if (marge <= 0)
                resultat.Critiques.Add(id);
        }

        resultat.FinProjet = finProjet;
        return resultat;
    }

    /// <summary>Date de début au plus tôt du successeur selon le type de liaison.</summary>
    private static int DebutMinimum(
        TypeLiaison type, int debutPredecesseur, int finPredecesseur, int dureeSuccesseur) => type switch
    {
        TypeLiaison.FinDebut => finPredecesseur + 1,
        TypeLiaison.DebutDebut => debutPredecesseur,
        TypeLiaison.FinFin => finPredecesseur - dureeSuccesseur + 1,
        TypeLiaison.DebutFin => debutPredecesseur - dureeSuccesseur + 1,
        _ => finPredecesseur + 1
    };

    /// <summary>Date de fin au plus tard du prédécesseur selon le type de liaison.</summary>
    private static int FinMaximum(
        TypeLiaison type, int debutSuccesseur, int finSuccesseur, int dureePredecesseur) => type switch
    {
        TypeLiaison.FinDebut => debutSuccesseur - 1,
        TypeLiaison.DebutDebut => debutSuccesseur + dureePredecesseur - 1,
        TypeLiaison.FinFin => finSuccesseur,
        TypeLiaison.DebutFin => finSuccesseur + dureePredecesseur - 1,
        _ => debutSuccesseur - 1
    };
}
