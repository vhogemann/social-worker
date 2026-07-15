using System.Collections.Generic;

namespace SocialWorker.Api.Features.Publishing.Validation;

public interface IValidateDraftRule<TSegment>
{
    IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<TSegment> context);
}