using FluentValidation;

namespace PlayMe.Application.Commands.RegisterPresence;

public sealed class RegisterPresenceCommandValidator : AbstractValidator<RegisterPresenceCommand>
{
    public RegisterPresenceCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
