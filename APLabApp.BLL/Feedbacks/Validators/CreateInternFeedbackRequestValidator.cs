using FluentValidation;

namespace APLabApp.BLL.Feedbacks
{
    public sealed class CreateInternFeedbackRequestValidator : AbstractValidator<CreateInternFeedbackRequest>
    {
        public CreateInternFeedbackRequestValidator()
        {
            RuleFor(x => x.ReceiverUserId)
                .NotEmpty().WithMessage("Receiver is required.");
            RuleFor(x => x.Comment)
                .NotEmpty().WithMessage("Feedback text is required.")
                .MaximumLength(2000).WithMessage("Feedback text must be at most 2000 characters.");
        }
    }
}
