namespace PlayMe.Application.Abstractions;

/// <summary>
/// Why a room was reaped and <c>RoomExpired</c> broadcast. Rides on the
/// event payload (docs/state.md §2.3) so the web client can explain the
/// deadline that actually fired — the unjoined 30-minute recruiting
/// window vs. an <c>ISetupGame</c> setup budget that neither player met.
/// Wire values are camelCase strings matching the Zod enum in
/// <c>packages/shared/src/realtime/schemas.ts</c>.
/// </summary>
public enum RoomExpiryReason
{
    /// <summary>
    /// The <c>WaitingForOpponent</c> window elapsed with no challenger
    /// (<c>RoomLifetimes.WaitingForOpponent</c>).
    /// </summary>
    Unjoined,

    /// <summary>
    /// A setup game's deadline elapsed with neither side committed
    /// (one-sided misses are a forfeit / <c>MatchEnded</c> instead).
    /// </summary>
    SetupTimeout,
}
