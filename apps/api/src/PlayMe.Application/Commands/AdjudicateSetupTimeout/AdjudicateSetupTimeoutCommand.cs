namespace PlayMe.Application.Commands.AdjudicateSetupTimeout;

/// <summary>
/// Dispatched by the setup-deadline sweeper (Sprint 10 seam C) when a
/// room's <c>playme:setup_deadlines</c> entry reaches its deadline. The
/// sweeper holds the room lock for the duration of the call.
/// </summary>
public sealed record AdjudicateSetupTimeoutCommand(string RoomCode);
