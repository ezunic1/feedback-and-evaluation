using FluentValidation;

namespace APLabApp.BLL.Seasons
{
    public class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
    {
        public CreateSeasonRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Season name is required.")
                .MinimumLength(2).WithMessage("Season name must be at least 2 characters long.");
            RuleFor(x => x.StartDate)
                .NotEqual(default(DateTime)).WithMessage("Start date is required.");
            RuleFor(x => x.EndDate)
                .NotEqual(default(DateTime)).WithMessage("End date is required.");
            RuleFor(x => x)
                .Must(x => x.StartDate != default && x.EndDate != default && x.StartDate < x.EndDate)
                .WithMessage("Start date must be before end date.");
        }
    }
}
