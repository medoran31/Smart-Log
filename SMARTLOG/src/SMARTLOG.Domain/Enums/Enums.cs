namespace Smartlog.Domain.Enums;

public enum StatutProjet
{
    EnCours,
    Termine,
    Suspendu
}

public enum TypeIntervenant
{
    MaitreOuvrage,
    MaitreOeuvre,
    Entreprise,
    BureauControle,
    Laboratoire,
    SousTraitant
}

public enum TypeAvenant
{
    Delai,
    Financier,
    Mixte
}

public enum StatutAvenant
{
    Brouillon,
    Valide,
    Annule
}

public enum StatutAttachement
{
    Brouillon,
    Contradictoire,
    Valide
}

public enum TypeSituation
{
    Provisoire,
    Definitive
}

public enum StatutSituation
{
    Brouillon,
    Emise,
    Payee
}

public enum PrioriteReserve
{
    Basse,
    Moyenne,
    Haute
}

public enum StatutReserve
{
    Nouvelle,
    EnCours,
    EnVerification,
    Levee,
    Cloturee
}

public enum SensCourrier
{
    Entrant,
    Sortant
}

public enum StatutCourrier
{
    EnAttente,
    Repondu,
    SansSuite
}

public enum PrioriteAlerte
{
    Basse,
    Moyenne,
    Haute
}
