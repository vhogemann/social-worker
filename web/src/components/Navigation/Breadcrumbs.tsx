import React from "react";
import { Link, useLocation } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronRight } from "@fortawesome/free-solid-svg-icons";
import { findDraft, useDraftStore } from "../../store/draftStore";
import { useTemplateEditorStore } from "../TemplateStudio/editor/templateEditorStore";

interface BreadcrumbItem {
  label: string;
  to?: string;
}

const SETTINGS_TAB_LABELS: Record<string, string> = {
  account: "Account",
  connections: "Connections",
  "brand-voices": "Brand Voices",
  feeds: "Feeds",
  users: "Users",
  providers: "LLM Providers",
};

export const Breadcrumbs: React.FC = () => {
  const location = useLocation();
  const path = location.pathname;

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const templateName = useTemplateEditorStore((s) => s.name);

  const getItems = (): BreadcrumbItem[] => {
    // 1. Workflow-scoped variant drafts: /workflows/:workflowId/draft/:draftId/variant/:variantId
    if (path.startsWith("/workflows/") && path.includes("/draft/") && path.includes("/variant/")) {
      const parts = path.split("/");
      const workflowId = parts[2];
      const canonicalDraftId = parts[4];
      const variantId = parts[6];

      const canonicalDraft = findDraft(drafts, canonicalDraftId) || drafts.find((d) => d.id === canonicalDraftId);
      const variantDraft = findDraft(drafts, variantId) || canonicalDraft?.variants?.find((v) => v.id === variantId);

      const workflowTitle = canonicalDraft?.automationWorkflowTitle || "Workflow";
      const canonicalTitle = canonicalDraft?.title || "Draft";
      const variantTitle =
        variantDraft?.targetPlatform ||
        (variantDraft?.title ? variantDraft.title.replace(/^.*\((\w+)\)$/, "$1") : "Variant");

      return [
        { label: "Workflows", to: "/" },
        { label: workflowTitle, to: `/workflows/${workflowId}` },
        { label: canonicalTitle, to: `/workflows/${workflowId}/draft/${canonicalDraftId}` },
        { label: variantTitle },
      ];
    }

    // 2. Workflow-scoped drafts: /workflows/:workflowId/draft or /workflows/:workflowId/draft/:draftId
    if (path.startsWith("/workflows/") && path.includes("/draft")) {
      const parts = path.split("/");
      const workflowId = parts[2];
      const draftId = parts[4];
      const draft = draftId
        ? findDraft(drafts, draftId) || (activeDraftId ? findDraft(drafts, activeDraftId) : null)
        : null;
      const workflowTitle = draft?.automationWorkflowTitle || "Workflow";
      const draftTitle = draft?.title || "Draft";

      return [
        { label: "Workflows", to: "/" },
        { label: workflowTitle, to: `/workflows/${workflowId}` },
        { label: draftTitle },
      ];
    }

    // 3. Workflows & Workflow Detail
    if (path === "/" || path === "/workflows") {
      return [{ label: "Workflows" }];
    }

    if (path.startsWith("/workflows/")) {
      const parts = path.split("/");
      const workflowId = parts[2];
      const matchingDraft = drafts.find((d) => d.automationWorkflowId === workflowId);
      const title = matchingDraft?.automationWorkflowTitle || "Workflow";
      return [
        { label: "Workflows", to: "/" },
        { label: title },
      ];
    }

    // 4. Standalone variant drafts: /draft/:draftId/variant/:variantId
    if (path.startsWith("/draft/") && path.includes("/variant/")) {
      const parts = path.split("/");
      const canonicalDraftId = parts[2];
      const variantId = parts[4];

      const canonicalDraft = findDraft(drafts, canonicalDraftId) || drafts.find((d) => d.id === canonicalDraftId);
      const variantDraft = findDraft(drafts, variantId) || canonicalDraft?.variants?.find((v) => v.id === variantId);

      const canonicalTitle = canonicalDraft?.title || "Draft";
      const variantTitle =
        variantDraft?.targetPlatform ||
        (variantDraft?.title ? variantDraft.title.replace(/^.*\((\w+)\)$/, "$1") : "Variant");

      return [
        { label: "Drafts", to: "/drafts" },
        { label: canonicalTitle, to: `/draft/${canonicalDraftId}` },
        { label: variantTitle },
      ];
    }

    // 5. Drafts & Standalone Composer
    if (path === "/drafts") {
      return [{ label: "Drafts" }];
    }

    if (path.startsWith("/draft/")) {
      const draftId = path.replace("/draft/", "").split("/")[0];
      const draft = findDraft(drafts, draftId) || (activeDraftId ? findDraft(drafts, activeDraftId) : null);
      const title = draft?.title || "Draft";
      return [
        { label: "Drafts", to: "/drafts" },
        { label: title },
      ];
    }

    if (path === "/composer") {
      return [
        { label: "Drafts", to: "/drafts" },
        { label: "Composer" },
      ];
    }

    // 6. Templates
    if (path === "/templates") {
      return [{ label: "Templates" }];
    }

    if (path === "/templates/editor") {
      return [
        { label: "Templates", to: "/templates" },
        { label: templateName || "New Template" },
      ];
    }

    if (path.startsWith("/templates/editor/")) {
      return [
        { label: "Templates", to: "/templates" },
        { label: templateName || "Template Editor" },
      ];
    }

    // 7. Sources
    if (path === "/sources") {
      return [{ label: "Sources" }];
    }

    // 8. Logs
    if (path === "/logs") {
      return [{ label: "Logs" }];
    }

    // 9. Privacy
    if (path === "/privacy") {
      return [{ label: "Privacy Policy" }];
    }

    // 10. Reply Agents
    if (path === "/reply-agents") {
      return [{ label: "Reply Agents" }];
    }
    if (path.startsWith("/reply-agents/")) {
      return [
        { label: "Reply Agents", to: "/reply-agents" },
        { label: "Agent Details" },
      ];
    }

    // 10. Settings & Sub-tabs
    if (path.startsWith("/settings")) {
      const tab = path.replace("/settings/", "").replace("/settings", "");
      const tabLabel = SETTINGS_TAB_LABELS[tab] || "Settings";
      return [
        { label: "Settings", to: "/settings/account" },
        { label: tabLabel },
      ];
    }

    return [];
  };

  const items = getItems();

  if (items.length === 0) {
    return null;
  }

  return (
    <nav aria-label="Breadcrumb" className="flex items-center text-xs text-muted">
      {items.map((item, index) => {
        const isLast = index === items.length - 1;

        return (
          <React.Fragment key={index}>
            {index > 0 && (
              <FontAwesomeIcon
                icon={faChevronRight}
                className="w-2.5 h-2.5 mx-1.5 text-muted-foreground/40 shrink-0"
              />
            )}
            {item.to && !isLast ? (
              <Link
                to={item.to}
                className="hover:text-foreground transition-colors truncate max-w-[160px]"
              >
                {item.label}
              </Link>
            ) : (
              <span className={`truncate max-w-[200px] ${isLast ? "text-foreground font-medium" : ""}`}>
                {item.label}
              </span>
            )}
          </React.Fragment>
        );
      })}
    </nav>
  );
};
