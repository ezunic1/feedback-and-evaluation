using System;
using FluentValidation;

namespace APLabApp.BLL.DeleteRequests
{
    public sealed class CreateDeleteRequestDtoValidator : AbstractValidator<CreateDeleteRequestDto>
    {
        public CreateDeleteRequestDtoValidator()
        {
            RuleFor(x => x.FeedbackId)
                .GreaterThan(0).WithMessage("Feedback is required.");
            RuleFor(x => x.SenderUserId)
                .NotEqual(Guid.Empty).WithMessage("Sender is required.");
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Reason is required.")
                .MaximumLength(1000).WithMessage("Reason must be at most 1000 characters.");
        }
    }
}
