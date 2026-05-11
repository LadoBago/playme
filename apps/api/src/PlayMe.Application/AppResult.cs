using System.Diagnostics.CodeAnalysis;
using PlayMe.Application.Errors;

namespace PlayMe.Application;

/// <summary>
/// Result of an Application handler. Per CLAUDE.md §8: throw domain
/// exceptions for invariants; return <see cref="AppResult{T}"/> for expected
/// failure paths. <c>Detail</c> is a small free-form string for logs/server-
/// side diagnostics — never shown to users (clients render the i18n key from
/// <see cref="ErrorCode"/>).
/// </summary>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Static factory methods on Result&lt;T&gt; are idiomatic.")]
[SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily",
    Justification = "Explicit 'default' on the Fail factory's value argument documents intent.")]
public sealed class AppResult<T>
{
    public bool Succeeded { get; }
    public T? Value { get; }
    public ErrorCode? Error { get; }
    public string? Detail { get; }

    private AppResult(bool succeeded, T? value, ErrorCode? error, string? detail)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        Detail = detail;
    }

    public static AppResult<T> Ok(T value) =>
        new(succeeded: true, value: value, error: null, detail: null);

    public static AppResult<T> Fail(ErrorCode error, string? detail = null) =>
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
        return AppResult<TOther>.Fail(Error!.Value, Detail);
    }
}

/// <summary>Marker for handlers that return no value but can still fail.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
