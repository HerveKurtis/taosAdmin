using System.Text.RegularExpressions;

namespace AdminTaos.Services;

/// <summary>
/// Traitement structurel d'un IBAN : on normalise ce qui entre, on refuse ce qui ne peut pas
/// en être un. Pas de contrôle de la clé modulo 97 — le coût dépasse le bénéfice ici, et un
/// IBAN structurellement correct mais faux reste rattrapable à la relecture d'un virement.
/// </summary>
public static class IbanFormat
{
    private static readonly Regex Shape =
        new("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", RegexOptions.Compiled);

    /// <summary>Majuscules, espaces supprimés. Une saisie vide devient null.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var compact = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        return compact.Length == 0 ? null : compact;
    }

    /// <summary>Vrai si la valeur est absente (c'est permis) ou structurellement un IBAN.</summary>
    public static bool IsAcceptable(string? normalized)
        => normalized is null || Shape.IsMatch(normalized);
}
