# MCP-First Operations Policy

## Principle
When interacting with `social-worker` operational data, workflows, drafts, accounts, feeds, schedules, posts, or reply agents, always use the registered `socialworker` MCP tools rather than executing direct SQL queries, raw shell scripts, or internal HTTP curl calls.

## Intent to MCP Tool Mapping

| Intent | MCP Tool | Purpose & Key Parameters |
| :--- | :--- | :--- |
| **Inspect published posts / engagement** | `list_posts` | Query published posts with comment counts and engagement metrics. Filters: `platform`, `workflowId`, `since`, `hasReplies`, `limit`. |
| **Inspect drafts** | `list_drafts` | Check thread composition drafts, stages, statuses, workflows. Filters: `workflowId`, `isPublished`, `status`, `stage`, `since`, `limit`. |
| **Inspect draft details** | `get_draft` | Retrieve segment content, attached sources, platform variants, and published thread links by `draftId`. |
| **Inspect comments & triage** | `list_comment_audits` | Query incoming platform comments, sentiment classification, ranking scores, agent decisions, and replies. Filters: `agentId`, `workflowId`, `platform`, `status`, `decision`, `since`, `limit`. |
| **Reply to comments** | `reply_to_comment` | Author and publish a reply or override an agent decision for a specific comment using `commentAuditId`. |
| **Trigger reply evaluation** | `trigger_reply_check` | Force an immediate polling check and LLM ranking cycle for comments on a workflow or agent (`workflowId` or `agentId`). |
| **Check reply agents** | `list_reply_agents`, `get_reply_agent` | List or inspect autonomous reply agent configurations, persona prompts, and assigned workflows. |
| **Configure reply agents** | `create_reply_agent`, `update_reply_agent`, `configure_reply_agent`, `assign_reply_agent` | Update persona instructions, ranking thresholds, maximum depth, or attach agents to workflows. |
| **Inspect workflows** | `list_workflows` | Check active automated content curation workflows and schedules. |
| **Trigger workflow** | `trigger_workflow` | Manually run a specific curation workflow by `workflowId`. |
| **Inspect feeds** | `list_feeds` | Check RSS/Atom feeds, health, last fetched dates. |
| **Trigger feed poll** | `trigger_feed_poll` | Poll feeds for fresh items (`feedSubscriptionId`). |
| **Inspect schedules & queue** | `list_schedules`, `get_schedule`, `get_schedule_queue`, `get_calendar_events` | Inspect upcoming publishing slots and queue items. |
| **Inspect accounts** | `list_accounts` | Verify connected social accounts and platform handles. |

## Fallbacks
Direct database queries via `psql` or shell scripts should only be used as a last-resort diagnostics fallback when an endpoint or MCP tool fails, is missing required diagnostic telemetry, or during database migrations.

## Complete Documentation & Schema Reference
For full schemas, parameters, response models, and platform constraints for all 39 tools, see [docs/mcp_server.md](../../docs/mcp_server.md).

