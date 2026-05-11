using FluentValidation;

namespace PlayMe.Application.Commands.SubmitMove;

public sealed class SubmitMoveCommandValidator : AbstractValidator<SubmitMoveCommand>
{
    public SubmitMoveCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
        RuleFor(x => x.Move).NotNull();
    }
}
