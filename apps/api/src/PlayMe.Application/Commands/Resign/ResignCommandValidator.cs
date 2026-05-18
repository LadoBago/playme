using FluentValidation;

namespace PlayMe.Application.Commands.Resign;

public sealed class ResignCommandValidator : AbstractValidator<ResignCommand>
{
    public ResignCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
