using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Unique point de calcul des valeurs financières dérivées d'une situation.
/// Les champs MontantCumule, RetenueGarantie, Avance et Penalites ne sont
/// jamais saisis ni modifiés ailleurs (règle 1 du projet).
/// </summary>
public class SituationService : ISituationService
{
    private static readonly Regex PeriodeRegex = new(@"^\d{4}-\d{2}$", RegexOptions.Compiled);

    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IAttachementService _attachements;

    public SituationService(
        SmartlogDbContext db,
        IJournalService journal,
        IAttachementService attachements)
    {
        _db = db;
        _journal = journal;
        _attachements = attachements;
    }

    public async Task InitialiserAsync(
        Guid situationId,
        decimal tauxAvancement,
        decimal tauxRetenueGarantie,
        Guid utilisateurId,
        decimal tauxAvance = 0.10m,
        CancellationToken ct = default)
    {
        VerifierTaux(tauxAvancement, nameof(tauxAvancement));
        VerifierTaux(tauxRetenueGarantie, nameof(tauxRetenueGarantie));
        VerifierTaux(tauxAvance, nameof(tauxAvance));

        var situation = await _db.Situations
            .Include(s => s.Projet)
            .FirstOrDefaultAsync(s => s.Id == situationId, ct)
            ?? throw new InvalidOperationException($"Situation {situationId} introuvable.");

        if (situation.MontantCumule != 0)
            return;

        var projet = situation.Projet!;
        var avant = new
        {
            situation.MontantCumule,
            situation.RetenueGarantie,
            situation.Avance,
            situation.Penalites
        };

        situation.MontantCumule = Math.Round(projet.MontantActualise * tauxAvancement, 2);
        situation.RetenueGarantie = Math.Round(situation.MontantCumule * tauxRetenueGarantie, 2);
        situation.Avance = Math.Round(situation.MontantCumule * tauxAvance, 2);
        situation.Penalites = 0m;

        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Situation), situation.Id, "CalculMontantsDerives",
            avant,
            new
            {
                situation.MontantCumule,
                situation.RetenueGarantie,
                situation.Avance,
                situation.Penalites
            },
            ct);
    }

    public async Task<SituationResume> CreerOuRecalculerAsync(
        Guid projetId,
        string periode,
        TypeSituation type,
        decimal tauxRetenueGarantie,
        decimal tauxAvance,
        decimal penalites,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var periodeValidee = VerifierPeriode(periode);

        var existante = await _db.Situations
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjetId == projetId && s.Periode == periodeValidee, ct);

        var situationId = existante?.Id;
        if (situationId is null)
        {
            var situation = new Situation
            {
                ProjetId = projetId,
                Periode = periodeValidee,
                Type = type,
                Statut = StatutSituation.Brouillon
            };
            _db.Situations.Add(situation);
            await _db.SaveChangesAsync(ct);
            situationId = situation.Id;

            await _journal.EnregistrerAsync(
                utilisateurId, nameof(Situation), situation.Id, "Creation", null,
                new { situation.Id, situation.ProjetId, situation.Periode, situation.Type, situation.Statut }, ct);
        }

        return await RecalculerAsync(
            situationId.Value, tauxRetenueGarantie, tauxAvance, penalites, utilisateurId, ct);
    }

    public async Task<SituationResume> RecalculerAsync(
        Guid situationId,
        decimal tauxRetenueGarantie,
        decimal tauxAvance,
        decimal penalites,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        VerifierTaux(tauxRetenueGarantie, nameof(tauxRetenueGarantie));
        VerifierTaux(tauxAvance, nameof(tauxAvance));
        if (penalites < 0)
            throw new ArgumentOutOfRangeException(nameof(penalites), "Les pénalités ne peuvent pas être négatives.");

        var situation = await _db.Situations
            .FirstOrDefaultAsync(s => s.Id == situationId, ct)
            ?? throw new InvalidOperationException($"Situation {situationId} introuvable.");

        var avant = new
        {
            situation.MontantCumule,
            situation.RetenueGarantie,
            situation.Avance,
            situation.Penalites
        };

        var (montantPeriode, montantCumule) =
            await CalculerMontantsAttachementsAsync(situation.ProjetId, situation.Periode, ct);

        situation.MontantCumule = montantCumule;
        situation.RetenueGarantie = Math.Round(montantCumule * tauxRetenueGarantie, 2);
        situation.Avance = Math.Round(montantCumule * tauxAvance, 2);
        situation.Penalites = Math.Round(penalites, 2);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Situation), situation.Id, "RecalculSituation",
            avant,
            new
            {
                MontantPeriode = montantPeriode,
                situation.MontantCumule,
                situation.RetenueGarantie,
                situation.Avance,
                situation.Penalites,
                NetAPayer = CalculerNetAPayer(situation)
            },
            ct);

        return await ObtenirAsync(situation.Id, ct)
            ?? throw new InvalidOperationException("Situation introuvable après recalcul.");
    }

    public async Task<SituationResume> ChangerStatutAsync(
        Guid situationId,
        StatutSituation statut,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var situation = await _db.Situations
            .FirstOrDefaultAsync(s => s.Id == situationId, ct)
            ?? throw new InvalidOperationException($"Situation {situationId} introuvable.");

        var avant = situation.Statut;
        situation.Statut = statut;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Situation), situation.Id, "ChangementStatut",
            new { Statut = avant.ToString() }, new { Statut = statut.ToString() }, ct);

        return await ObtenirAsync(situation.Id, ct)
            ?? throw new InvalidOperationException("Situation introuvable après changement de statut.");
    }

    public async Task<SituationResume?> ObtenirAsync(Guid situationId, CancellationToken ct = default)
    {
        var situation = await _db.Situations
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == situationId, ct);
        if (situation is null)
            return null;

        var (montantPeriode, _) =
            await CalculerMontantsAttachementsAsync(situation.ProjetId, situation.Periode, ct);
        return VersResume(situation, montantPeriode);
    }

    public async Task<SuiviFinancier> ObtenirSuiviAsync(Guid projetId, CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projetId, ct)
            ?? throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var situations = await _db.Situations
            .AsNoTracking()
            .Where(s => s.ProjetId == projetId)
            .OrderBy(s => s.Periode)
            .ToListAsync(ct);

        var resumes = new List<SituationResume>();
        foreach (var situation in situations)
        {
            var (montantPeriode, _) =
                await CalculerMontantsAttachementsAsync(projetId, situation.Periode, ct);
            resumes.Add(VersResume(situation, montantPeriode));
        }

        var evolution = new List<PointEvolution>();
        foreach (var resume in resumes)
        {
            var engage = resume.MontantCumule;
            var paye = resumes
                .Where(s => s.StatutValeur == StatutSituation.Payee &&
                            string.CompareOrdinal(s.Periode, resume.Periode) <= 0)
                .Sum(s => Math.Max(0m, s.NetAPayer));
            evolution.Add(new PointEvolution(resume.Periode, engage, paye, engage - paye));
        }

        var dernier = evolution.LastOrDefault();
        return new SuiviFinancier(
            projetId,
            projet.MontantActualise,
            resumes,
            evolution,
            dernier?.MontantEngage ?? 0m,
            dernier?.MontantPaye ?? 0m,
            dernier?.RestantDu ?? 0m);
    }

    private async Task<(decimal Periode, decimal Cumule)> CalculerMontantsAttachementsAsync(
        Guid projetId,
        string periode,
        CancellationToken ct)
    {
        var debut = DebutPeriode(periode);
        var fin = debut.AddMonths(1).AddDays(-1);

        var datesValidees = await _db.Attachements
            .AsNoTracking()
            .Where(a => a.ProjetId == projetId && a.Statut == StatutAttachement.Valide)
            .Select(a => new { a.Id, a.Date })
            .ToListAsync(ct);

        if (datesValidees.Count == 0)
            return (0m, 0m);

        var datesParId = datesValidees.ToDictionary(a => a.Id, a => a.Date);
        var lignes = await _attachements.ListerLignesAsync(projetId, ct);
        var montants = lignes
            .Where(l => datesParId.TryGetValue(l.AttachementId, out var date))
            .Select(l => new { Date = datesParId[l.AttachementId], l.MontantCalcule })
            .ToList();

        return (
            Math.Round(montants.Where(m => m.Date >= debut && m.Date <= fin).Sum(m => m.MontantCalcule), 2),
            Math.Round(montants.Where(m => m.Date <= fin).Sum(m => m.MontantCalcule), 2));
    }

    private static SituationResume VersResume(Situation situation, decimal montantPeriode) =>
        new(
            situation.Id,
            situation.ProjetId,
            situation.Periode,
            situation.Type == TypeSituation.Definitive ? "Définitive" : "Provisoire",
            LibelleStatut(situation.Statut),
            situation.Statut,
            montantPeriode,
            situation.MontantCumule,
            situation.RetenueGarantie,
            situation.Avance,
            situation.Penalites,
            CalculerNetAPayer(situation));

    private static decimal CalculerNetAPayer(Situation situation) =>
        Math.Round(
            situation.MontantCumule - situation.RetenueGarantie - situation.Avance - situation.Penalites, 2);

    private static string LibelleStatut(StatutSituation statut) => statut switch
    {
        StatutSituation.Brouillon => "Brouillon",
        StatutSituation.Emise => "Émise",
        StatutSituation.Payee => "Payée",
        _ => statut.ToString()
    };

    private static string VerifierPeriode(string periode)
    {
        if (string.IsNullOrWhiteSpace(periode) || !PeriodeRegex.IsMatch(periode.Trim()) ||
            !DateOnly.TryParseExact(periode.Trim() + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ArgumentException("La période doit être au format AAAA-MM.", nameof(periode));

        return periode.Trim();
    }

    private static DateOnly DebutPeriode(string periode) =>
        DateOnly.ParseExact(periode + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void VerifierTaux(decimal taux, string nom)
    {
        if (taux < 0 || taux > 1)
            throw new ArgumentOutOfRangeException(nom, "Le taux doit être compris entre 0 et 1.");
    }
}
