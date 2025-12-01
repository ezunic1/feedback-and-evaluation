using FluentValidation;

namespace APLabApp.BLL.Users
{
    public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
    {
        private static readonly string[] AllowedRoles = new[] { "admin", "mentor", "intern", "guest" };

        public CreateUserRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Desc).MaximumLength(2000).When(x => x.Desc != null);
            RuleFor(x => x.RoleName)
                .Must(r => string.IsNullOrWhiteSpace(r) || AllowedRoles.Contains(r.Trim().ToLower()))
                .WithMessage("RoleName must be one of: admin, mentor, intern, guest.");
            When(x => !string.IsNullOrWhiteSpace(x.RoleName) && x.RoleName!.Trim().ToLower() != "intern", () =>
            {
                RuleFor(x => x.SeasonId).Null().WithMessage("SeasonId can be set only for users with role 'intern'.");
            });
            When(x => !string.IsNullOrWhiteSpace(x.Password), () =>
            {
                RuleFor(x => x.Password!).MinimumLength(6);
            });
        }
    }
}
