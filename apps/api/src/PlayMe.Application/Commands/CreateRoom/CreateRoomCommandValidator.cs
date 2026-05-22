using FluentValidation;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Surface-level validation for <see cref="CreateRoomCommand"/>. Display-
/// name and side semantics get a second-line check in the handler
/// (<c>DisplayName.Create</c>, side-vs-mode rules) because they need the
/// resolved game module — those produce typed <c>PlatformErrors</c> keys rather than
/// FluentValidation faults.
/// </summary>
public sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>
    /// Max raw JSON size of the <c>gameOptions</c> blob persisted with the
    /// room. Surface cap — the game module performs schema validation, but
    /// without a size cap a malicious caller could attach megabytes of
    /// "padding" that the module accepts (only inspecting known fields)
    /// and Redis ends up storing it. 1 KiB comfortably fits every realistic
    /// per-game options shape.
    /// </summary>
    public const int MaxGameOptionsRawJsonLength = 1024;

    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.HostDisplayName)
            .NotEmpty()
            .MaximumLength(DisplayName.MaxLength * 4); // pre-sanitization cap (allow utf-8 bloat)

        RuleFor(x => x.GameId).NotEmpty();

        RuleFor(x => x.SideSelectionMode).IsInEnum();

        RuleFor(x => x.GameOptions)
            .Must(options =>
                options is null || options.Value.GetRawText().Length <= MaxGameOptionsRawJsonLength)
            .WithMessage($"gameOptions raw JSON exceeds {MaxGameOptionsRawJsonLength} bytes.");
    }
}
