using FluentValidation;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Surface-level validation for <see cref="CreateRoomCommand"/>. Display-
/// name and side semantics get a second-line check in the handler
/// (<c>DisplayName.Create</c>, side-vs-mode rules) because they need the
/// resolved game module — those produce typed <c>ErrorCode</c>s rather than
/// FluentValidation faults.
/// </summary>
public sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.HostDisplayName)
            .NotEmpty()
            .MaximumLength(DisplayName.MaxLength * 4); // pre-sanitization cap (allow utf-8 bloat)

        RuleFor(x => x.GameId).NotEmpty();

        RuleFor(x => x.SideSelectionMode).IsInEnum();
    }
}
