using FluentValidation;

namespace PlayMe.Application.Commands.OfferRematch;

public sealed class OfferRematchCommandValidator : AbstractValidator<OfferRematchCommand>
{
    public OfferRematchCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
