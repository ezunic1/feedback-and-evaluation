using FluentValidation;

namespace APLabApp.BLL.Feedbacks
{
    public sealed class CreateMentorFeedbackRequestValidator : AbstractValidator<CreateMentorFeedbackRequest>
    {
        public CreateMentorFeedbackRequestValidator()
        {
            RuleFor(x => x.ReceiverUserId)
                .NotEmpty().WithMessage("Receiver is required.");
            RuleFor(x => x.Comment)
                .NotEmpty().WithMessage("Feedback text is required.")
                .MaximumLength(2000).WithMessage("Feedback text must be at most 2000 characters.");
            RuleFor(x => x.CareerSkills)
                .InclusiveBetween(1, 5).WithMessage("Career skills must be between 1 and 5.");
            RuleFor(x => x.Communication)
                .InclusiveBetween(1, 5).WithMessage("Communication must be between 1 and 5.");
            RuleFor(x => x.Collaboration)
                .InclusiveBetween(1, 5).WithMessage("Collaboration must be between 1 and 5.");
        }
    }
}
