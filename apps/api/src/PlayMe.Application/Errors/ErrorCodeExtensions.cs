using System.Reflection;

namespace PlayMe.Application.Errors;

public static class ErrorCodeExtensions
{
    private static readonly Dictionary<ErrorCode, string> _i18nKeys = BuildMap();

    /// <summary>
    /// The canonical i18n key for this code (CLAUDE.md §3). Throws if the
    /// enum value is missing its <see cref="I18nKeyAttribute"/> — a startup-
    /// time programmer error.
    /// </summary>
    public static string ToI18nKey(this ErrorCode code) =>
        _i18nKeys.TryGetValue(code, out var key)
            ? key
            : throw new InvalidOperationException(
                $"ErrorCode.{code} is missing an [I18nKey] attribute.");

    private static Dictionary<ErrorCode, string> BuildMap()
    {
        var map = new Dictionary<ErrorCode, string>();
        var type = typeof(ErrorCode);
        foreach (var code in Enum.GetValues<ErrorCode>())
        {
            var field = type.GetField(code.ToString())
                ?? throw new InvalidOperationException($"ErrorCode.{code} field not found.");
            var attr = field.GetCustomAttribute<I18nKeyAttribute>();
            if (attr is null)
            {
                throw new InvalidOperationException(
                    $"ErrorCode.{code} is missing an [I18nKey] attribute.");
            }
            map[code] = attr.Key;
        }
        return map;
    }
}
