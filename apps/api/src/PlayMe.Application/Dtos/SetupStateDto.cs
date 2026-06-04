namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of the setup-phase commitment flags (Sprint 10 seam C).
/// Present on <see cref="MatchDto"/> only for games whose module
/// implements <c>ISetupGame</c> — setup-less games keep their wire shape
/// unchanged (the null field is omitted from the JSON). Carries role-level
/// readiness only; the setup payloads themselves live inside the opaque
/// per-game state and, for hidden-state games, never reach the opponent.
/// </summary>
public sealed record SetupStateDto(
    bool HostCommitted,
    bool ChallengerCommitted);
