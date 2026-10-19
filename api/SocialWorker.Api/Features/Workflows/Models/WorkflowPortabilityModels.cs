using System;
using System.Collections.Generic;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Workflows;

public sealed record ExportedFeedDefinition(
    string Title,
    string FeedUrl,
    string? WebsiteUrl = null,
    string? IncludeFilters = null,
    string? ExcludeFilters = null);

public sealed record ExportedWorkflowSource(
    WorkflowSourceKind Kind,
    ExportedFeedDefinition? Feed = null,
    string? ConfigurationJson = null);

public sealed record ExportedWorkflowDestination(
    string Platform,
    bool AutoPublish = false,
    WorkflowPublishingMode PublishingMode = WorkflowPublishingMode.Manual);

public sealed record ExportedWorkflow(
    string Title,
    string InstructionPrompt,
    int MinRelevanceScore,
    List<ExportedWorkflowSource> Sources,
    List<ExportedWorkflowDestination> Destinations);

public sealed record WorkflowExportPackage(
    int Version,
    DateTimeOffset ExportedAt,
    List<ExportedWorkflow> Workflows);

public sealed record WorkflowImportResult(
    int ImportedWorkflowsCount,
    int CreatedFeedsCount,
    int ReusedFeedsCount,
    List<Guid> ImportedWorkflowIds);
