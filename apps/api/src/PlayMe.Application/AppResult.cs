using System.Diagnostics.CodeAnalysis;

namespace PlayMe.Application;

/// <summary>
/// Result of an Application handler. Per CLAUDE.md §8: throw domain
/// exceptions for invariants; return <see cref="AppResult{T}"/> for expected
/// failure paths. <see cref="Error"/> is the i18n key the client renders;
/// platform-owned keys live in <see cref="Errors.PlatformErrors"/>, per-game
/// keys are an agreement between the per-game server module and the per-game
/// web renderer (CLAUDE.md §7 "Platform thinness"). <see cref="Detail"/> is a
/// small free-form string for logs / server-side diagnostics — never shown to
/// users.
/// </summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory methods on Result&lt;T&gt; are idiomatic.")]
[SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily",
    Justification = "Explicit 'default' on the Fail factory's value argument documents intent.")]
public sealed class AppResult<T>
{
    public bool Succeeded { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? Detail { get; }

    private AppResult(bool succeeded, T? value, string? error, string? detail)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        Detail = detail;
    }

    public static AppResult<T> Ok(T value) =>
        new(succeeded: true, value: value, error: null, detail: null);

    public static AppResult<T> Fail(string error, string? detail = null) =>
        new(succeeded: false, value: default, error: error, detail: detail);

    /// <summary>
    /// Propagate this failure into a result of a different value type.
    /// Throws if called on a successful result — programmer error.
    /// </summary>
    public AppResult<TOther> ToFailure<TOther>()
    {
        if (Succeeded)
        {
            throw new InvalidOperationException(
                "ToFailure called on a successful result.");
        }
        return AppResult<TOther>.Fail(Error!, Detail);
    }
}

