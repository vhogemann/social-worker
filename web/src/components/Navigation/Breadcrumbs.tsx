import React from "react";
import { Link, useLocation } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronRight } from "@fortawesome/free-solid-svg-icons";
import { useDraftStore } from "../../store/draftStore";
import { useWorkflowStore } from "../../store/workflowStore";
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
  const activeWorkflow = useWorkflowStore((s) => s.activeWorkflow);
  const templateName = useTemplateEditorStore((s) => s.name);

  const getItems = (): BreadcrumbItem[] => {
    // 1. Workflow-scoped drafts: /workflows/:workflowId/draft or /workflows/:workflowId/draft/:draftId
    if (path.startsWith("/workflows/") && path.includes("/draft")) {
      const parts = path.split("/");
      const workflowId = parts[2];
      const draftId = parts[4];
      const draft = draftId
        ? drafts.find((d) => d.id === draftId) || (activeDraftId ? drafts.find((d) => d.id === activeDraftId) : null)
        : null;
      const workflowTitle = activeWorkflow?.title || draft?.automationWorkflowTitle || "Workflow";
      const draftTitle = draft?.title || "Draft";

      return [
        { label: "Workflows", to: "/" },
        { label: workflowTitle, to: `/workflows/${workflowId}` },
        { label: draftTitle }
      ];
    }

    // 2. Workflows & Workflow Detail
    if (path === "/" || path === "/workflows") {
      return [{ label: "Workflows" }];
    }

    if (path.startsWith("/workflows/")) {
      const title = activeWorkflow?.title || "Detail";
      return [
        { label: "Workflows", to: "/" },
        { label: title }
      ];
    }

    // 3. Drafts & Standalone Composer
    if (path === "/drafts") {
      return [{ label: "Drafts" }];
    }

    if (path.startsWith("/draft/")) {
      const draftId = path.replace("/draft/", "").split("/")[0];
      const draft = drafts.find((d) => d.id === draftId) || (activeDraftId ? drafts.find((d) => d.id === activeDraftId) : null);
      const title = draft?.title || "Draft";
      return [
        { label: "Drafts", to: "/drafts" },
        { label: title }
      ];
    }

    if (path === "/composer") {
      return [
        { label: "Drafts", to: "/drafts" },
        { label: "Composer" }
      ];
    }

    // 4. Templates
    if (path === "/templates") {
      return [{ label: "Templates" }];
    }

    if (path === "/templates/editor") {
      return [
        { label: "Templates", to: "/templates" },
        { label: templateName || "New Template" }
      ];
    }

    if (path.startsWith("/templates/editor/")) {
      return [
        { label: "Templates", to: "/templates" },
        { label: templateName || "Template Editor" }
      ];
    }

    // 5. Settings
    if (path.startsWith("/settings")) {
      const tab = path.replace("/settings/", "").replace("/settings", "") || "account";
      const tabLabel = SETTINGS_TAB_LABELS[tab] || "Settings";
      if (path === "/settings" || path === "/settings/account") {
        return [{ label: "Settings", to: "/settings/account" }, { label: "Account" }];
      }
      return [
        { label: "Settings", to: "/settings/account" },
        { label: tabLabel }
      ];
    }

    // 6. Standalone pages
    if (path === "/sources") return [{ label: "Sources Library" }];
    if (path === "/logs") return [{ label: "Logs" }];
    if (path === "/privacy") return [{ label: "Privacy" }];

    return [];
  };

  const items = getItems();
  if (items.length <= 1) return null;

  return (
    <nav aria-label="Breadcrumb" className="flex items-center gap-1.5 text-xs">
      {items.map((item, index) => {
        const isLast = index === items.length - 1;
        return (
          <React.Fragment key={index}>
            {index > 0 && (
              <FontAwesomeIcon
                icon={faChevronRight}
                className="w-2.5 h-2.5 text-muted/60"
              />
            )}
            {item.to && !isLast ? (
              <Link
                to={item.to}
                className="hover:text-foreground transition max-w-[150px] truncate"
              >
                {item.label}
              </Link>
            ) : (
              <span
                className={`${
                  isLast ? "text-foreground font-medium" : "text-muted"
                } max-w-[180px] truncate`}
              >
                {item.label}
              </span>
            )}
          </React.Fragment>
        );
      })}
    </nav>
  );
};
