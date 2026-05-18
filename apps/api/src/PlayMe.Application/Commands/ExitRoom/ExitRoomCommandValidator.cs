using FluentValidation;

namespace PlayMe.Application.Commands.ExitRoom;

public sealed class ExitRoomCommandValidator : AbstractValidator<ExitRoomCommand>
{
    public ExitRoomCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();
        RuleFor(x => x.CallerPlayerId).NotEmpty();
        RuleFor(x => x.CallerRole).IsInEnum();
    }
}
