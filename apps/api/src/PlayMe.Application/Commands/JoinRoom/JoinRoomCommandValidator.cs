using FluentValidation;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.JoinRoom;

public sealed class JoinRoomCommandValidator : AbstractValidator<JoinRoomCommand>
{
    public JoinRoomCommandValidator()
    {
        RuleFor(x => x.RoomCode).NotEmpty();

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(DisplayName.MaxLength * 4);
    }
}
