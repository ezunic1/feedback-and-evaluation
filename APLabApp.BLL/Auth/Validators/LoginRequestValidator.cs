using FluentValidation;

namespace APLabApp.BLL.Auth
{
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.UsernameOrEmail)
                .NotEmpty().WithMessage("Email or username is required.");
            When(x => (x.UsernameOrEmail ?? string.Empty).Contains('@'), () =>
            {
                RuleFor(x => x.UsernameOrEmail!)
                    .EmailAddress().WithMessage("Enter a valid email address.");
            });
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.");
        }
    }
}
