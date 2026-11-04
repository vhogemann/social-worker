import React, { useEffect, useState, useMemo } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowLeft,
  faSliders,
  faRss,
  faShareNodes,
  faPenFancy,
  faSpinner
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchWorkflow,
  fetchWorkflows,
  createWorkflow,
  updateWorkflow,
  type AutomationWorkflowDto,
  type WorkflowSourceKind,
  type CreateWorkflowSourceRequest
} from "../../api/workflows";
import { fetchFeeds, type FeedSubscriptionDto } from "../../api/feeds";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { fetchSchedules, type ScheduleSummaryDto } from "../../api/schedules";
import { Input } from "../common/Input";
import { Button } from "../common/Button";
import { AlertBanner } from "../common/AlertBanner";
import { WorkflowSourcesSelector } from "./WorkflowSourcesSelector";
import {
  WorkflowDestinationsSelector,
  type WorkflowDestinationItem,
  type PublishingMode
} from "./WorkflowDestinationsSelector";

export type WorkflowConfigTab = "general" | "relevance" | "sources" | "destinations";

interface WorkflowTabConfig {
  id: WorkflowConfigTab;
  label: string;
  icon: typeof faPenFancy;
  title: string;
  description: string;
}

const TABS: WorkflowTabConfig[] = [
  {
    id: "general",
    label: "General",
    icon: faPenFancy,
    title: "General Configuration",
    description: "Workflow name and autonomous 2-tier generation prompt instructions."
  },
  {
    id: "relevance",
    label: "Quality & Filtering",
    icon: faSliders,
    title: "Quality & Content Relevance Threshold",
    description: "Minimum score required for ingested feed items to become drafts."
  },
  {
    id: "sources",
    label: "Ingestion Sources",
    icon: faRss,
    title: "Ingestion Sources",
    description: "Choose RSS/YouTube feeds or aggregate highlights from other workflows."
  },
  {
    id: "destinations",
    label: "Destinations",
    icon: faShareNodes,
    title: "Publishing Destinations & Automation",
    description: "Configure target platforms, publishing modes, and scheduling slots."
  }
];

export const WorkflowEditorPage: React.FC = () => {
  const { id } = useParams<{ id?: string }>();
  const navigate = useNavigate();
  const isEditMode = Boolean(id);

  const [activeTab, setActiveTab] = useState<WorkflowConfigTab>("general");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Workflow Form State
  const [title, setTitle] = useState("");
  const [instructionPrompt, setInstructionPrompt] = useState(
    "Summarize this article as an engaging thread."
  );
  const [sourceKind, setSourceKind] = useState<WorkflowSourceKind>("Feed");
  const [selectedFeedIds, setSelectedFeedIds] = useState<string[]>([]);
  const [selectedSourceWorkflowIds, setSelectedSourceWorkflowIds] = useState<string[]>([]);
  const [lookbackDays, setLookbackDays] = useState(7);
  const [destinations, setDestinations] = useState<WorkflowDestinationItem[]>([]);
  const [workflowScheduleId, setWorkflowScheduleId] = useState<string | null>(null);
  const [minRelevanceScore, setMinRelevanceScore] = useState<number>(7);

  // Reference data
  const [workflow, setWorkflow] = useState<AutomationWorkflowDto | null>(null);
  const [availableWorkflows, setAvailableWorkflows] = useState<AutomationWorkflowDto[]>([]);
  const [availableFeeds, setAvailableFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [connectedAccounts, setConnectedAccounts] = useState<AccountDto[]>([]);
  const [availableSchedules, setAvailableSchedules] = useState<ScheduleSummaryDto[]>([]);

  useEffect(() => {
    let isMounted = true;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const [feeds, accounts, allWorkflows, schedulesList] = await Promise.all([
          fetchFeeds().catch(() => [] as FeedSubscriptionDto[]),
          listAccounts().catch(() => [] as AccountDto[]),
          fetchWorkflows().catch(() => [] as AutomationWorkflowDto[]),
          fetchSchedules().catch(() => [] as ScheduleSummaryDto[])
        ]);

        if (!isMounted) return;
        setAvailableFeeds(feeds);
        setConnectedAccounts(accounts);
        setAvailableWorkflows(allWorkflows);
        setAvailableSchedules(schedulesList);

        if (id) {
          const wf = await fetchWorkflow(id);
          if (!isMounted) return;
          setWorkflow(wf);
          setTitle(wf.title);
          setInstructionPrompt(wf.instructionPrompt || "");
          setWorkflowScheduleId(wf.scheduleId || null);
          setMinRelevanceScore(wf.minRelevanceScore ?? 7);

          const hasHighlights = wf.sources.some((s) => s.kind === "WorkflowHighlights");
          if (hasHighlights) {
            setSourceKind("WorkflowHighlights");
            const hlSources = wf.sources.filter((s) => s.kind === "WorkflowHighlights");
            const wfIds: string[] = [];
            let days = 7;
            for (const s of hlSources) {
              if (s.sourceWorkflowId) wfIds.push(s.sourceWorkflowId);
              if (s.configurationJson) {
                try {
                  const parsed = JSON.parse(s.configurationJson);
                  if (parsed.lookbackDays) days = parsed.lookbackDays;
                  if (Array.isArray(parsed.sourceWorkflowIds)) {
                    wfIds.push(...parsed.sourceWorkflowIds);
                  }
                } catch {}
              }
            }
            setSelectedSourceWorkflowIds(Array.from(new Set(wfIds)));
            setLookbackDays(days);
            setSelectedFeedIds([]);
          } else {
            setSourceKind("Feed");
            setSelectedFeedIds(
              wf.sources
                .map((s) => s.feedSubscriptionId)
                .filter((feedId): feedId is string => Boolean(feedId))
            );
            setSelectedSourceWorkflowIds([]);
            setLookbackDays(7);
          }

          setDestinations(
            wf.destinations.map((d) => ({
              platform: d.platform,
              autoPublish: d.autoPublish,
              publishingMode: d.publishingMode || (d.autoPublish ? "Automatic" : "Manual"),
              scheduleId: d.scheduleId,
              accountId: d.accountId ?? undefined,
              accountHandle: d.accountHandle ?? undefined
            }))
          );
        } else {
          setTitle("");
          setInstructionPrompt("Summarize this article as an engaging thread.");
          setSourceKind("Feed");
          setSelectedFeedIds(feeds.map((f) => f.id));
          setSelectedSourceWorkflowIds([]);
          setLookbackDays(7);
          setWorkflowScheduleId(null);
          setMinRelevanceScore(7);
          if (accounts.length > 0) {
            setDestinations(
              accounts.map((a) => ({
                platform: a.platform,
                autoPublish: false,
                publishingMode: "Manual",
                accountId: a.id,
                accountHandle: a.handle
              }))
            );
          } else {
            setDestinations([{ platform: "Bluesky", autoPublish: false, publishingMode: "Manual" }]);
          }
        }
      } catch (err: any) {
        if (isMounted) {
          setError(err?.message || "Failed to load workflow configuration.");
        }
      } finally {
        if (isMounted) {
          setLoading(false);
        }
      }
    };

    void load();
    return () => {
      isMounted = false;
    };
  }, [id]);

  const activeTabConfig = useMemo(() => {
    return TABS.find((t) => t.id === activeTab) || TABS[0];
  }, [activeTab]);

  const handleToggleFeed = (feedId: string) => {
    setSelectedFeedIds((prev) =>
      prev.includes(feedId) ? prev.filter((item) => item !== feedId) : [...prev, feedId]
    );
  };

  const handleToggleSourceWorkflow = (workflowId: string) => {
    setSelectedSourceWorkflowIds((prev) =>
      prev.includes(workflowId) ? prev.filter((item) => item !== workflowId) : [...prev, workflowId]
    );
  };

  const handleToggleDestination = (
    platform: string,
    accountId?: string,
    accountHandle?: string
  ) => {
    setDestinations((prev) => {
      const exists = prev.some((d) =>
        accountId ? d.accountId === accountId : d.platform === platform
      );
      if (exists) {
        return prev.filter((d) =>
          accountId ? d.accountId !== accountId : d.platform !== platform
        );
      } else {
        return [
          ...prev,
          {
            platform,
            autoPublish: false,
            publishingMode: "Manual",
            accountId,
            accountHandle
          }
        ];
      }
    });
  };

  const handleChangePublishingMode = (
    platform: string,
    mode: PublishingMode,
    scheduleId?: string,
    accountId?: string
  ) => {
    setDestinations((prev) =>
      prev.map((d) => {
        const matches = accountId ? d.accountId === accountId : d.platform === platform;
        if (!matches) return d;
        return {
          ...d,
          publishingMode: mode,
          autoPublish: mode === "Automatic",
          scheduleId:
            mode === "Scheduled"
              ? scheduleId || (availableSchedules.length > 0 ? availableSchedules[0].id : null)
              : null
        };
      })
    );
  };

  const handleSave = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!title.trim()) {
      setActiveTab("general");
      setError("Workflow title is required.");
      return;
    }

    setSaving(true);
    setError(null);
    try {
      let sourcesPayload: CreateWorkflowSourceRequest[] = [];
      if (sourceKind === "Feed") {
        sourcesPayload = selectedFeedIds.map((feedId) => ({
          kind: "Feed" as const,
          feedSubscriptionId: feedId
        }));
      } else {
        sourcesPayload = selectedSourceWorkflowIds.map((wfId) => ({
          kind: "WorkflowHighlights" as const,
          sourceWorkflowId: wfId,
          configurationJson: JSON.stringify({ lookbackDays, sourceWorkflowIds: [wfId] })
        }));
      }

      const payload = {
        title: title.trim(),
        instructionPrompt: instructionPrompt.trim() || undefined,
        feedSubscriptionIds: sourceKind === "Feed" ? selectedFeedIds : [],
        sources: sourcesPayload,
        destinations: destinations.map((d) => ({
          platform: d.platform,
          autoPublish: d.autoPublish,
          publishingMode: d.publishingMode,
          scheduleId: d.scheduleId,
          accountId: d.accountId,
          accountHandle: d.accountHandle
        })),
        scheduleId: workflowScheduleId,
        minRelevanceScore
      };

      if (isEditMode && id) {
        await updateWorkflow(id, payload);
        navigate(`/workflows/${id}`);
      } else {
        const created = await createWorkflow(payload);
        navigate(created?.id ? `/workflows/${created.id}` : "/");
      }
    } catch (err: any) {
      setError(err?.message || "Failed to save workflow.");
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    if (isEditMode && id) {
      navigate(`/workflows/${id}`);
    } else {
      navigate("/");
    }
  };

  if (loading) {
    return (
      <div className="flex-1 min-h-0 flex flex-col items-center justify-center bg-bg text-muted gap-3">
        <FontAwesomeIcon icon={faSpinner} spin className="text-2xl text-accent" />
        <span className="text-xs font-mono">Loading workflow configuration...</span>
      </div>
    );
  }

  return (
    <div className="flex-1 min-h-0 flex overflow-hidden font-sans bg-bg" data-testid="workflow-editor-page">
      {/* Sidebar navigation (matching SettingsPage) */}
      <aside className="w-64 bg-panel border-r border-border p-4 flex flex-col justify-between select-none shrink-0 overflow-y-auto">
        <div className="space-y-6">
          <div className="space-y-1">
            <Link
              to={isEditMode && id ? `/workflows/${id}` : "/"}
              className="inline-flex items-center gap-2 text-xs font-mono text-muted hover:text-foreground transition mb-2"
            >
              <FontAwesomeIcon icon={faArrowLeft} className="w-3 h-3" />
              <span>{isEditMode ? "Back to Workflow" : "Back to Workflows"}</span>
            </Link>
            <div className="px-3 text-xs font-mono uppercase tracking-wider text-muted font-bold">
              Workflow Config
            </div>
            {workflow && (
              <div className="px-3 text-xs text-foreground font-semibold truncate">
                {workflow.title}
              </div>
            )}
          </div>

          <div className="space-y-4">
            <div>
              <div className="px-3 text-[10px] font-mono uppercase tracking-wider text-muted/80 font-semibold mb-2">
                Sections
              </div>
              <nav className="space-y-1">
                {TABS.map((t) => {
                  const isActive = activeTab === t.id;
                  let badge = null;
                  if (t.id === "sources") {
                    badge = sourceKind === "Feed" ? selectedFeedIds.length : selectedSourceWorkflowIds.length;
                  } else if (t.id === "destinations") {
                    badge = destinations.length;
                  } else if (t.id === "relevance") {
                    badge = minRelevanceScore === 0 ? "Off" : `${minRelevanceScore}/10`;
                  }

                  return (
                    <button
                      key={t.id}
                      type="button"
                      onClick={() => setActiveTab(t.id)}
                      className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-mono transition-colors text-left ${
                        isActive
                          ? "bg-accent/15 text-accent font-medium border border-accent/30"
                          : "text-muted hover:text-foreground hover:bg-border/20"
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <FontAwesomeIcon icon={t.icon} className="w-3.5 h-3.5 shrink-0" />
                        <span>{t.label}</span>
                      </div>
                      {badge !== null && (
                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-panel border border-border text-muted">
                          {badge}
                        </span>
                      )}
                    </button>
                  );
                })}
              </nav>
            </div>
          </div>
        </div>

        {/* Sidebar Footer: Summary */}
        <div className="space-y-3 pt-4 border-t border-border">
          <div className="px-3 py-2 border border-border rounded-lg bg-bg/40 text-xs font-mono">
            <div className="text-[10px] text-muted uppercase tracking-wider mb-0.5">Configuration Summary</div>
            <div className="text-foreground text-[11px] truncate">
              {sourceKind === "Feed" ? `${selectedFeedIds.length} feeds` : "Highlights mode"} · {destinations.length} destination{destinations.length === 1 ? "" : "s"}
            </div>
            <div className="text-muted text-[10px] mt-1">
              Threshold: {minRelevanceScore === 0 ? "Off" : `${minRelevanceScore}/10`}
            </div>
          </div>
        </div>
      </aside>

      {/* Main Content Area (matching SettingsPage) */}
      <main className="flex-1 flex flex-col h-full overflow-hidden bg-bg">
        <div className="px-8 py-6 border-b border-border bg-panel/30 shrink-0 flex justify-between items-center">
          <div>
            <h1 className="text-xl font-bold text-foreground tracking-tight">
              {isEditMode ? "Edit Automation Workflow" : "Create Automation Workflow"}
            </h1>
            <p className="text-xs text-muted mt-1">{activeTabConfig.description}</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="secondary" size="sm" onClick={handleCancel} disabled={saving}>
              Cancel
            </Button>
            <Button variant="primary" size="sm" loading={saving} onClick={() => handleSave()}>
              {isEditMode ? "Save Changes" : "Create Workflow"}
            </Button>
          </div>
        </div>

        <div className="flex-1 p-8 overflow-y-auto min-h-0">
          <div className="max-w-4xl mx-auto flex flex-col gap-6">
            {error && (
              <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />
            )}

            {/* TAB 1: General */}
            {activeTab === "general" && (
              <div className="flex flex-col gap-6">
                <Input
                  required
                  label="Workflow Title"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="e.g. Daily Tech Highlights / RSS to Bluesky"
                />

                <div className="flex flex-col">
                  <div className="flex justify-between items-center mb-1.5">
                    <label
                      htmlFor="workflow-instruction-prompt"
                      className="block text-[10px] font-mono uppercase tracking-wider text-muted"
                    >
                      Custom Instruction Prompt (Optional)
                    </label>
                    <span className="text-[10px] font-mono text-muted">
                      {instructionPrompt.length} characters
                    </span>
                  </div>
                  <textarea
                    id="workflow-instruction-prompt"
                    value={instructionPrompt}
                    onChange={(e) => setInstructionPrompt(e.target.value)}
                    placeholder="Instructions for drafting the thread (e.g. tone, structure, bullet points, key takeaways)..."
                    className="w-full min-h-[380px] rounded-lg border border-border bg-bg px-3.5 py-3 text-xs font-mono leading-relaxed text-foreground outline-none transition focus:border-accent resize-y shadow-inner"
                  />
                  <p className="mt-2 text-[11px] text-muted">
                    These system instructions guide the 2-tier LLM generation pipeline when synthesizing ingested sources into drafts. You can specify persona voice, hashtag rules, thread pacing, and call-to-action styles.
                  </p>
                </div>
              </div>
            )}

            {/* TAB 2: Quality & Filtering */}
            {activeTab === "relevance" && (
              <Box
                sx={{
                  bgcolor: "action.hover",
                  border: 1,
                  borderColor: "divider",
                  borderRadius: 2.5,
                  p: 3.5,
                  display: "flex",
                  flexDirection: "column",
                  gap: 2.5
                }}
              >
                <div className="flex justify-between items-center">
                  <div>
                    <label
                      htmlFor="workflow-relevance-threshold"
                      className="block text-sm font-bold text-foreground"
                    >
                      Relevance & Impact Threshold
                    </label>
                    <Typography
                      variant="caption"
                      sx={{ color: "text.secondary", fontSize: "0.75rem", display: "block" }}
                    >
                      Items below this score are automatically discarded.
                    </Typography>
                  </div>
                  <span
                    className={`font-mono text-xs px-2.5 py-1 rounded-md border font-semibold ${
                      minRelevanceScore === 0
                        ? "bg-zinc-500/10 text-muted border-border"
                        : minRelevanceScore >= 8
                        ? "bg-red-500/10 text-red-400 border-red-500/30"
                        : minRelevanceScore >= 6
                        ? "bg-accent/10 text-accent border-accent/30"
                        : "bg-blue-500/10 text-blue-400 border-blue-500/30"
                    }`}
                    data-testid="relevance-score-badge"
                  >
                    {minRelevanceScore === 0 ? "Off (0/10)" : `${minRelevanceScore}/10`}
                  </span>
                </div>

                <div className="flex flex-col gap-2">
                  <input
                    id="workflow-relevance-threshold"
                    type="range"
                    min={0}
                    max={10}
                    step={1}
                    value={minRelevanceScore}
                    onChange={(e) => setMinRelevanceScore(parseInt(e.target.value, 10))}
                    data-testid="relevance-score-slider"
                    className="w-full accent-accent cursor-pointer"
                  />
                  <div className="flex justify-between text-[10px] font-mono text-muted">
                    <span>0 (Off)</span>
                    <span>3 (Low)</span>
                    <span>5 (Moderate)</span>
                    <span className="text-accent font-semibold">7 (High Impact)</span>
                    <span>10 (Breaking Only)</span>
                  </div>
                </div>

                <p className="text-[11px] text-muted italic bg-bg/60 p-3 rounded border border-border/50">
                  {minRelevanceScore === 0 &&
                    "Filter disabled: All ingested feed items will be processed into drafts."}
                  {minRelevanceScore > 0 &&
                    minRelevanceScore <= 4 &&
                    "Permissive: Ingests almost everything, including routine administrative updates and minor news."}
                  {minRelevanceScore >= 5 &&
                    minRelevanceScore <= 6 &&
                    "Moderate: Filters out trivial announcements but allows routine daily political commentary."}
                  {minRelevanceScore === 7 &&
                    "High Impact (Recommended): Filters routine filler, preserving publishing slots for major exposés, viral debates, and economic consequences."}
                  {minRelevanceScore >= 8 &&
                    "Strict: Discards everything except top-tier breaking crises and high-impact investigative scoops."}
                </p>
              </Box>
            )}

            {/* TAB 3: Ingestion Sources */}
            {activeTab === "sources" && (
              <WorkflowSourcesSelector
                sourceKind={sourceKind}
                onSourceKindChange={setSourceKind}
                availableFeeds={availableFeeds}
                selectedFeedIds={selectedFeedIds}
                onToggleFeed={handleToggleFeed}
                availableWorkflows={availableWorkflows.filter((w) => !id || w.id !== id)}
                selectedSourceWorkflowIds={selectedSourceWorkflowIds}
                onToggleSourceWorkflow={handleToggleSourceWorkflow}
                lookbackDays={lookbackDays}
                onLookbackDaysChange={setLookbackDays}
              />
            )}

            {/* TAB 4: Destinations */}
            {activeTab === "destinations" && (
              <WorkflowDestinationsSelector
                connectedAccounts={connectedAccounts}
                destinations={destinations}
                availableSchedules={availableSchedules}
                onToggleDestination={handleToggleDestination}
                onChangePublishingMode={handleChangePublishingMode}
              />
            )}
          </div>
        </div>
      </main>
    </div>
  );
};
