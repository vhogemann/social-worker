import React from "react";
import { Link, useLocation, useParams } from "react-router-dom";
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
    // 1. Workflows
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

    // 2. Composer / Drafts
    if (path === "/composer") {
      return [{ label: "Composer" }];
    }

    if (path.startsWith("/draft/")) {
      const draftId = path.replace("/draft/", "").split("/")[0];
      const draft = drafts.find((d) => d.id === draftId) || (activeDraftId ? drafts.find((d) => d.id === activeDraftId) : null);
      const title = draft?.title || "Draft";
      return [
        { label: "Composer", to: "/composer" },
        { label: title }
      ];
    }

    // 3. Templates
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

    // 4. Settings
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

    // 5. Standalone
    if (path === "/sources") return [{ label: "Sources Library" }];
    if (path === "/logs") return [{ label: "Logs" }];
    if (path === "/privacy") return [{ label: "Privacy" }];

    return [];
  };

  const items = getItems();

  if (items.length === 0) return null;

  return (
    <nav aria-label="Breadcrumbs" className="flex items-center gap-1.5 text-xs font-mono select-none" data-testid="breadcrumbs">
      <span className="text-border">/</span>
      {items.map((item, idx) => {
        const isLast = idx === items.length - 1;
        return (
          <React.Fragment key={idx}>
            {item.to && !isLast ? (
              <Link
                to={item.to}
                className="text-muted hover:text-foreground transition truncate max-w-[180px]"
              >
                {item.label}
              </Link>
            ) : (
              <span
                className={`truncate max-w-[220px] ${
                  isLast ? "text-foreground font-medium" : "text-muted"
                }`}
              >
                {item.label}
              </span>
            )}
            {!isLast && (
              <span className="text-border">/</span>
            )}
          </React.Fragment>
        );
      })}
    </nav>
  );
};
