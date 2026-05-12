using System.Globalization;
using System.Text;

namespace PlayMe.Domain.Platform;

/// <summary>
/// A player's chosen name for the session (CLAUDE.md §2.7, §5.3): max 24
/// chars after sanitization, with control characters, zero-width characters,
/// and RTL/LTR override codepoints stripped before storing or echoing.
///
/// Construction sanitizes-then-validates: callers may pass raw user input and
/// will get either a clean value or an <see cref="ArgumentException"/> if the
/// input is empty after sanitization or exceeds the length cap. Application
/// validators (FluentValidation) check up front for nice error codes; this
/// type enforces the invariant defensively.
/// </summary>
public readonly record struct DisplayName
{
    public const int MaxLength = 24;

    public string Value { get; }

    private DisplayName(string sanitized)
    {
        Value = sanitized;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Sanitize and validate raw user input. Throws if the result is empty
    /// or exceeds <see cref="MaxLength"/> chars (measured in text elements
    /// after sanitization, so an emoji counts as one).
    /// </summary>
    public static DisplayName Create(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var sanitized = Sanitize(raw);

        if (sanitized.Length == 0)
        {
            throw new ArgumentException(
                "DisplayName must be non-empty after sanitization.", nameof(raw));
        }

        var elements = new StringInfo(sanitized).LengthInTextElements;
        if (elements > MaxLength)
        {
            throw new ArgumentException(
                $"DisplayName must be at most {MaxLength} characters.", nameof(raw));
        }

        return new DisplayName(sanitized);
    }

    /// <summary>
    /// Strip control chars (Cc, Cf), zero-width spacing chars, and the
    /// RTL/LTR/PDF override codepoints that can be used to spoof rendering.
    /// Whitespace is collapsed and trimmed.
    /// </summary>
    private static string Sanitize(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var lastWasSpace = false;

        var enumerator = StringInfo.GetTextElementEnumerator(raw);
        while (enumerator.MoveNext())
        {
            var element = (string)enumerator.Current;
            if (IsStripped(element))
            {
                continue;
            }

            if (IsSpace(element))
            {
                if (sb.Length == 0 || lastWasSpace)
                {
                    continue;
                }
                sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(element);
                lastWasSpace = false;
            }
        }

        if (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length -= 1;
        }

        return sb.ToString();
    }

    private static bool IsStripped(string element)
    {
        // Single-rune fast path covers >99% of inputs.
        if (element.Length <= 2 && Rune.TryGetRuneAt(element, 0, out var rune))
        {
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate
                or UnicodeCategory.OtherNotAssigned)
            {
                return true;
            }

            // Specifically reject bidi overrides and zero-width chars even
            // when their Unicode category would otherwise let them through.
            return rune.Value switch
            {
                0x200B or 0x200C or 0x200D or 0xFEFF => true, // ZWSP, ZWNJ, ZWJ, BOM
                0x202A or 0x202B or 0x202C or 0x202D or 0x202E => true, // bidi overrides
                0x2066 or 0x2067 or 0x2068 or 0x2069 => true, // bidi isolates
                _ => false,
            };
        }

        return false;
    }

    private static bool IsSpace(string element)
    {
        if (element.Length == 1)
        {
            return char.IsWhiteSpace(element[0]);
        }
        return false;
    }
}
