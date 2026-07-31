namespace Chiniseapp.Domain.Rules;

public static class EntryPublicationRules
{
    public static IReadOnlyList<string> Validate(
        string? lemma,
        int homonymCount,
        int gramGroupCount,
        int posCount,
        int senseCount,
        IReadOnlyList<string?> definitions)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(lemma))
        {
            errors.Add("A lemma is required before publication.");
        }

        if (senseCount < 1)
        {
            errors.Add("At least one sense is required before publication.");
        }

        if (homonymCount > 0 && gramGroupCount < homonymCount)
        {
            errors.Add("Every homonym must contain at least one grammar group before publication.");
        }

        if (gramGroupCount > 0 && posCount < gramGroupCount)
        {
            errors.Add("Every grammar group must have exactly one POS before publication.");
        }

        if (senseCount > 0 &&
            (definitions.Count != senseCount || definitions.Any(string.IsNullOrWhiteSpace)))
        {
            errors.Add("Every sense must have a non-empty definition before publication.");
        }

        return errors;
    }

    public static IReadOnlyList<string> Validate(
        string? lemma,
        int homonymCount,
        int senseCount,
        IReadOnlyList<string?> definitions) =>
        Validate(lemma, homonymCount, homonymCount, homonymCount, senseCount, definitions);
}
