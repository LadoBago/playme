using FluentValidation;

namespace PlayMe.Application.Commands.AcceptRematch;

public sealed class AcceptRematchCommandValidator : AbstractValidator<AcceptRematchCommand>
{
    public AcceptRematchCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
