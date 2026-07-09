using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ProposeStageTransitionArgs(string Platform, string Stage, string Reasoning);

public sealed record ProposeStageTransitionResult(bool Success, string Platform, string ProposedStage, string Reasoning);

public sealed class ProposeStageTransitionTool : ChatToolBase<ProposeStageTransitionArgs, ProposeStageTransitionResult>
{
    public override string Name => "propose_stage_transition";
    public override string Description => "Propose transitioning the draft to a new stage (e.g. Sourcing, Refining, Formatting, Ready, Sent).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "platform": {
              "type": "string",
              "description": "The target platform to adapt and transition (e.g. Bluesky, Twitter)."
            },
            "stage": {
              "type": "string",
              "description": "The target stage to propose: Draft, Ready, Sent."
            },
            "reasoning": {
              "type": "string",
              "description": "The rationale for proposing this stage transition."
            }
          },
          "required": ["platform", "stage", "reasoning"]
        }
        """).RootElement.Clone();

    public override Task<ProposeStageTransitionResult> ExecuteAsync(ProposeStageTransitionArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        return Task.FromResult(new ProposeStageTransitionResult(true, args.Platform, args.Stage, args.Reasoning));
    }
}
