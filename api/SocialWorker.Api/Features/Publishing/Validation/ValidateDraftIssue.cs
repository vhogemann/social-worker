namespace SocialWorker.Api.Features.Publishing.Validation;

public sealed record ValidateDraftIssue(ValidateDraftSeverity Severity, string Message);