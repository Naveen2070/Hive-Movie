using FluentValidation;
using Hive_Movie.DTOs;
namespace Hive_Movie.Validators.Auditorium;

public class CreateAuditoriumRequestValidator : AbstractValidator<CreateAuditoriumRequest>
{
    public CreateAuditoriumRequestValidator()
    {
        RuleFor(x => x.CinemaId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaxRows).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.MaxColumns).GreaterThan(0).LessThanOrEqualTo(1000);

        // CROSS-PROPERTY VALIDATION: Ensure disabled seats are within room bounds
        RuleForEach(x => x.Layout.DisabledSeats)
            .Must((request, seat) => seat.Row >= 0 && seat.Row < request.MaxRows)
            .WithMessage("A disabled seat's Row coordinate exceeds the auditorium's MaxRows.")
            .Must((request, seat) => seat.Col >= 0 && seat.Col < request.MaxColumns)
            .WithMessage("A disabled seat's Column coordinate exceeds the auditorium's MaxColumns.");

        // CROSS-PROPERTY VALIDATION: Ensure wheelchair spots are within room bounds
        RuleForEach(x => x.Layout.WheelchairSpots)
            .Must((request, seat) => seat.Row >= 0 && seat.Row < request.MaxRows)
            .WithMessage("A wheelchair spot's Row coordinate exceeds the auditorium's MaxRows.")
            .Must((request, seat) => seat.Col >= 0 && seat.Col < request.MaxColumns)
            .WithMessage("A wheelchair spot's Column coordinate exceeds the auditorium's MaxColumns.");

        // TIER VALIDATION
        RuleForEach(x => x.Layout.Tiers).ChildRules(tier =>
        {
            tier.RuleFor(t => t.TierName).NotEmpty().MaximumLength(100);
            tier.RuleFor(t => t.PriceSurcharge).GreaterThanOrEqualTo(0)
                .WithMessage("Price surcharge cannot be negative.");
        });

        // CROSS-PROPERTY TIER SEAT BOUNDARY CHECKS
        RuleForEach(x => x.Layout.Tiers)
            .Must((request, tier) => tier.Seats.All(s => s.Row >= 0 && s.Row < request.MaxRows))
            .WithMessage("One or more tier seats have a Row coordinate exceeding the auditorium's MaxRows.")
            .Must((request, tier) => tier.Seats.All(s => s.Col >= 0 && s.Col < request.MaxColumns))
            .WithMessage("One or more tier seats have a Column coordinate exceeding the auditorium's MaxColumns.");
    }
}