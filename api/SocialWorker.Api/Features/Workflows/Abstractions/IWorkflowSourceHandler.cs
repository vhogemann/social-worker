using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Workflows.Abstractions;

public interface IWorkflowSourceHandler
{
    WorkflowSourceKind Kind { get; }
    Task<int> ProcessAsync(AutomationWorkflow workflow, AutomationWorkflowSource source, CancellationToken ct);
}
