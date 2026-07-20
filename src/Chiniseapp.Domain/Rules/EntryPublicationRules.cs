namespace Chiniseapp.Domain.Rules;

public static class EntryPublicationRules
{
    public static IReadOnlyList<string> Validate(
        string? lemma,
        int homonymCount,
        int senseCount,
        IReadOnlyList<string?> definitions)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(lemma))
        {
            errors.Add("A lemma is required before publication.");
        }

        if (homonymCount < 1 || senseCount < 1)
        {
            errors.Add("At least one homonym with one sense is required before publication.");
        }

        if (senseCount > 0 &&
            (definitions.Count != senseCount || definitions.Any(string.IsNullOrWhiteSpace)))
        {
            errors.Add("Every sense must have a non-empty definition before publication.");
        }

        return errors;
    }
}
