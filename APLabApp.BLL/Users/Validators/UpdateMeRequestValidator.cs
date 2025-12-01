using FluentValidation;

namespace APLabApp.BLL.Users
{
    public class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
    {
        public UpdateMeRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(100);
            RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        }
    }
}
