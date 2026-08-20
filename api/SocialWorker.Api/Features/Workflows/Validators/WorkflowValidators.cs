using System;
using FluentValidation;

namespace SocialWorker.Api.Features.Workflows.Validators;

public sealed class CreateWorkflowDestinationRequestValidator : AbstractValidator<CreateWorkflowDestinationRequest>
{
    public CreateWorkflowDestinationRequestValidator()
    {
        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required.")
            .MaximumLength(50).WithMessage("Platform cannot exceed 50 characters.");
    }
}

public sealed class CreateAutomationWorkflowRequestValidator : AbstractValidator<CreateAutomationWorkflowRequest>
{
    public CreateAutomationWorkflowRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleForEach(x => x.Destinations)
            .SetValidator(new CreateWorkflowDestinationRequestValidator());
    }
}
