using FluentValidation;

namespace APLabApp.BLL.Users
{
    public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        private static readonly string[] AllowedRoles = new[] { "admin", "mentor", "intern", "guest" };

        public UpdateUserRequestValidator()
        {
            RuleFor(x => x.FullName).MinimumLength(2).MaximumLength(100).When(x => x.FullName != null);
            RuleFor(x => x.Email).EmailAddress().When(x => x.Email != null);
            RuleFor(x => x.Desc).MaximumLength(2000).When(x => x.Desc != null);
            RuleFor(x => x.RoleName)
                .Must(r => string.IsNullOrWhiteSpace(r) || AllowedRoles.Contains(r!.Trim().ToLower()))
                .WithMessage("RoleName must be one of: admin, mentor, intern, guest.");
            When(x => !string.IsNullOrWhiteSpace(x.RoleName) && x.RoleName!.Trim().ToLower() != "intern", () =>
            {
                RuleFor(x => x.SeasonId).Equal((int?)null).WithMessage("SeasonId can be set only for users with role 'intern'.");
            });
        }
    }
}
