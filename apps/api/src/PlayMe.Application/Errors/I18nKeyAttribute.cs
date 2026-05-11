namespace PlayMe.Application.Errors;

/// <summary>
/// Pins an <see cref="ErrorCode"/> enum value to its canonical i18n key
/// (CLAUDE.md §3). The mapping <c>ErrorCode.&lt;EnumValue&gt;</c> ↔
/// <c>errors.&lt;category&gt;.&lt;camelCase&gt;</c> is the source of truth;
/// the matching translations live in <c>packages/shared/i18n/{ka,en}.json</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class I18nKeyAttribute : Attribute
{
    public string Key { get; }
    public I18nKeyAttribute(string key) { Key = key; }
}
