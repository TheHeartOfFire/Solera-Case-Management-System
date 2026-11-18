using AMFormsCST.Core.Interfaces.UserSettings;
using System;
using System.Linq;

namespace AMFormsCST.Core.Utils;

/// <summary>
/// Parses user profile fields and a customizable signature template into a clean email signature.
/// </summary>
public static class UserSignatureFormatter
{
    public const string DefaultAutomateFormsSignature =
        "Thank You,\n{FirstName} {LastInitial}.\n{JobTitle}\nAuto/Mate F&I Forms";

    public static string FormatSignature(IUserSettings? userSettings)
    {
        if (userSettings == null)
        {
            return "Thank You,\nAuto/Mate F&I Forms";
        }

        var template = string.IsNullOrWhiteSpace(userSettings.EmailSignatureTemplate)
            ? DefaultAutomateFormsSignature
            : userSettings.EmailSignatureTemplate;

        return FormatSignature(
            template,
            userSettings.Name,
            userSettings.Title,
            userSettings.Email,
            userSettings.Phone);
    }

    public static string FormatSignature(
        string? template,
        string? fullName,
        string? jobTitle,
        string? email,
        string? phone)
    {
        var effectiveTemplate = string.IsNullOrWhiteSpace(template)
            ? DefaultAutomateFormsSignature
            : template;

        var nameParts = (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
        var lastName = nameParts.Length > 1 ? nameParts[^1] : string.Empty;
        var lastInitial = !string.IsNullOrEmpty(lastName) ? lastName[0].ToString() : string.Empty;

        var normalized = effectiveTemplate.Replace("\r\n", "\n");

        // Replace tokens
        var replaced = normalized
            .Replace("{FirstName}", firstName, StringComparison.OrdinalIgnoreCase)
            .Replace("{LastName}", lastName, StringComparison.OrdinalIgnoreCase)
            .Replace("{LastInitial}", lastInitial, StringComparison.OrdinalIgnoreCase)
            .Replace("{FullName}", fullName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Name}", fullName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{JobTitle}", jobTitle?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Title}", jobTitle?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Email}", email?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Phone}", phone?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Clean up lines that became empty or have dangling punctuation like " ." when LastInitial is missing
        var cleanedLines = replaced
            .Split('\n')
            .Select(line =>
            {
                var trimmed = line.Trim();
                // If line was "{FirstName} {LastInitial}." and LastInitial was empty -> "{FirstName} ." -> "{FirstName}"
                if (string.IsNullOrEmpty(lastInitial) && trimmed.EndsWith(" .", StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^2].TrimEnd();
                }
                else if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastInitial) && trimmed == ".")
                {
                    trimmed = string.Empty;
                }
                return trimmed;
            })
            .Where(line => !string.IsNullOrWhiteSpace(line));

        return string.Join(Environment.NewLine, cleanedLines);
    }
}

