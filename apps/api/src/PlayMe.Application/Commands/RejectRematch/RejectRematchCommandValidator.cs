using FluentValidation;

namespace PlayMe.Application.Commands.RejectRematch;

public sealed class RejectRematchCommandValidator : AbstractValidator<RejectRematchCommand>
{
    public RejectRematchCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
