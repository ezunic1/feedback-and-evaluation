using FluentValidation;

namespace APLabApp.BLL.Seasons
{
    public class UpdateSeasonRequestValidator : AbstractValidator<UpdateSeasonRequest>
    {
        public UpdateSeasonRequestValidator()
        {
            RuleFor(x => x.Name!).MinimumLength(2).MaximumLength(100).When(x => x.Name != null);
            RuleFor(x => x.StartDate!.Value).NotEqual(default(DateTime)).When(x => x.StartDate.HasValue);
            RuleFor(x => x.EndDate!.Value).NotEqual(default(DateTime)).When(x => x.EndDate.HasValue);
            RuleFor(x => x).Must(x =>
            {
                if (x.StartDate.HasValue && x.EndDate.HasValue)
                    return x.StartDate.Value < x.EndDate.Value;
                return true;
            })
            .WithMessage("StartDate must be before EndDate.");
        }
    }
}
