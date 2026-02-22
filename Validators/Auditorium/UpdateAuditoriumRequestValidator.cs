using FluentValidation;
using Hive_Movie.DTOs;

namespace Hive_Movie.Validators.Auditorium;

public class UpdateAuditoriumRequestValidator : AbstractValidator<UpdateAuditoriumRequest>
{
    public UpdateAuditoriumRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaxRows).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.MaxColumns).GreaterThan(0).LessThanOrEqualTo(1000);

        RuleForEach(x => x.Layout.DisabledSeats)
            .Must((request, seat) => seat.Row >= 0 && seat.Row < request.MaxRows)
            .WithMessage("A disabled seat's Row coordinate exceeds the auditorium's MaxRows.")
            .Must((request, seat) => seat.Col >= 0 && seat.Col < request.MaxColumns)
            .WithMessage("A disabled seat's Column coordinate exceeds the auditorium's MaxColumns.");

        RuleForEach(x => x.Layout.WheelchairSpots)
            .Must((request, seat) => seat.Row >= 0 && seat.Row < request.MaxRows)
            .WithMessage("A wheelchair spot's Row coordinate exceeds the auditorium's MaxRows.")
            .Must((request, seat) => seat.Col >= 0 && seat.Col < request.MaxColumns)
            .WithMessage("A wheelchair spot's Column coordinate exceeds the auditorium's MaxColumns.");
    }
}