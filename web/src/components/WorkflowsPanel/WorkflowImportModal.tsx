import React, { useState, useId } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCloudArrowUp,
  faFileImport,
  faCheck,
  faLayerGroup,
  faRss,
  faShareNodes,
} from "@fortawesome/free-solid-svg-icons";
import { Modal } from "../common/Modal";
import { Button } from "../common/Button";
import { AlertBanner } from "../common/AlertBanner";
import {
  importWorkflows,
  type WorkflowExportPackage,
  type WorkflowImportResult,
} from "../../api/workflows";

export interface WorkflowImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (result: WorkflowImportResult) => void;
}

export const WorkflowImportModal: React.FC<WorkflowImportModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const fileInputId = useId();
  const [jsonText, setJsonText] = useState("");
  const [parsedPackage, setParsedPackage] = useState<WorkflowExportPackage | null>(null);
  const [parseError, setParseError] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [importResult, setImportResult] = useState<WorkflowImportResult | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  const resetState = () => {
    setJsonText("");
    setParsedPackage(null);
    setParseError(null);
    setImporting(false);
    setImportError(null);
    setImportResult(null);
    setIsDragging(false);
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const validateAndSetJson = (text: string) => {
    setJsonText(text);
    setImportError(null);
    setImportResult(null);

    const trimmed = text.trim();
    if (!trimmed) {
      setParsedPackage(null);
      setParseError(null);
      return;
    }

    try {
      const parsed = JSON.parse(trimmed) as WorkflowExportPackage;
      if (!parsed || typeof parsed !== "object" || !Array.isArray(parsed.workflows)) {
        setParsedPackage(null);
        setParseError("Invalid workflow package: missing 'workflows' list.");
        return;
      }

      setParsedPackage(parsed);
      setParseError(null);
    } catch {
      setParsedPackage(null);
      setParseError("Invalid JSON syntax.");
    }
  };

  const handleFile = (file: File) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result;
      if (typeof content === "string") {
        validateAndSetJson(content);
      }
    };
    reader.readAsText(file);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) {
      handleFile(file);
    }
  };

  const handleImport = async () => {
    if (!parsedPackage) return;
    setImporting(true);
    setImportError(null);

    try {
      const result = await importWorkflows(parsedPackage);
      setImportResult(result);
      onSuccess(result);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to import workflows";
      setImportError(message);
    } finally {
      setImporting(false);
    }
  };

  // Derive counts from parsedPackage
  const workflowCount = parsedPackage?.workflows?.length ?? 0;
  const feedCount = parsedPackage?.workflows?.reduce((acc, wf) => {
    const feeds = wf.sources?.filter((s) => s.kind === "Feed" && s.feed?.feedUrl) ?? [];
    return acc + feeds.length;
  }, 0) ?? 0;

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title="Import Workflows"
      description="Import automation workflows and feed definitions from a JSON export package without credentials."
      maxWidth="lg"
      testId="workflow-import-modal"
      footer={
        <div className="flex justify-end gap-2 w-full">
          {importResult ? (
            <Button variant="primary" size="sm" onClick={handleClose}>
              Done
            </Button>
          ) : (
            <>
              <Button variant="secondary" size="sm" onClick={handleClose}>
                Cancel
              </Button>
              <Button
                variant="primary"
                size="sm"
                icon={faFileImport}
                disabled={!parsedPackage || workflowCount === 0 || Boolean(parseError)}
                loading={importing}
                onClick={handleImport}
              >
                {workflowCount > 0
                  ? `Import ${workflowCount} Workflow${workflowCount > 1 ? "s" : ""}`
                  : "Import Workflows"}
              </Button>
            </>
          )}
        </div>
      }
    >
      <div className="space-y-4">
        {importError && (
          <AlertBanner variant="error" message={importError} onDismiss={() => setImportError(null)} />
        )}

        {importResult ? (
          <div className="bg-emerald-500/10 border border-emerald-500/20 rounded-xl p-5 text-center space-y-3">
            <div className="w-10 h-10 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center mx-auto">
              <FontAwesomeIcon icon={faCheck} className="text-lg" />
            </div>
            <h3 className="text-sm font-semibold text-foreground">Import Complete</h3>
            <p className="text-xs text-muted max-w-sm mx-auto">
              Successfully imported {importResult.importedWorkflowsCount} workflow
              {importResult.importedWorkflowsCount !== 1 ? "s" : ""}.
            </p>
            <div className="flex justify-center gap-4 text-xs font-mono pt-2 border-t border-emerald-500/10">
              <span className="text-muted">
                Created {importResult.createdFeedsCount} new feed{importResult.createdFeedsCount !== 1 ? "s" : ""}
              </span>
              <span className="text-muted">•</span>
              <span className="text-muted">
                Reused {importResult.reusedFeedsCount} existing feed{importResult.reusedFeedsCount !== 1 ? "s" : ""}
              </span>
            </div>
          </div>
        ) : (
          <>
            {/* File Dropzone */}
            <div
              onDragOver={(e) => {
                e.preventDefault();
                setIsDragging(true);
              }}
              onDragLeave={() => setIsDragging(false)}
              onDrop={handleDrop}
              className={`border-2 border-dashed rounded-xl p-5 text-center transition-colors cursor-pointer ${
                isDragging
                  ? "border-accent bg-accent/5"
                  : "border-border hover:border-zinc-600 bg-bg/40"
              }`}
              onClick={() => document.getElementById(fileInputId)?.click()}
            >
              <input
                id={fileInputId}
                type="file"
                accept=".json,application/json"
                className="hidden"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) handleFile(file);
                }}
              />
              <FontAwesomeIcon icon={faCloudArrowUp} className="text-xl text-muted mb-2" />
              <p className="text-xs font-medium text-foreground">
                Drop your workflow JSON package here, or{" "}
                <span className="text-accent underline">choose a JSON file</span>
              </p>
              <p className="text-[11px] text-muted mt-1 font-mono">
                Only workflow definitions and feed URLs are imported; credentials and accounts are omitted
              </p>
            </div>

            {/* Paste JSON Fallback */}
            <div>
              <label
                htmlFor="workflow-json-paste"
                className="block text-xs font-mono text-muted uppercase tracking-wider mb-1"
              >
                Or Paste JSON
              </label>
              <textarea
                id="workflow-json-paste"
                value={jsonText}
                onChange={(e) => validateAndSetJson(e.target.value)}
                placeholder='Paste exported JSON here: {"version": 1, "workflows": [...]}'
                rows={5}
                className="w-full bg-bg border border-border rounded-lg p-2.5 text-xs font-mono text-foreground focus:outline-none focus:border-accent transition resize-none"
              />
              {parseError && (
                <p className="text-xs text-red-400 mt-1">{parseError}</p>
              )}
            </div>

            {/* Package Preview */}
            {parsedPackage && !parseError && (
              <div className="bg-bg/60 border border-border rounded-xl p-4 space-y-3">
                <div className="flex items-center justify-between border-b border-border/50 pb-2">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-semibold text-foreground">Package Preview</span>
                    <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-panel border border-border text-muted">
                      Package v{parsedPackage.version}
                    </span>
                  </div>
                  <span className="text-xs font-mono text-muted">
                    {workflowCount} workflow{workflowCount !== 1 ? "s" : ""} found
                  </span>
                </div>

                <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
                  {parsedPackage.workflows.map((wf, idx) => (
                    <div
                      key={idx}
                      className="bg-panel border border-border/60 rounded-lg p-2.5 flex items-center justify-between text-xs"
                    >
                      <div className="min-w-0 flex-1 mr-3">
                        <div className="font-medium text-foreground truncate">{wf.title}</div>
                        <div className="text-[11px] text-muted truncate mt-0.5">
                          {wf.instructionPrompt}
                        </div>
                      </div>
                      <div className="flex items-center gap-2 shrink-0 text-[10px] font-mono text-muted">
                        <span className="inline-flex items-center gap-1">
                          <FontAwesomeIcon icon={faRss} className="text-accent" />
                          {wf.sources?.length ?? 0}
                        </span>
                        <span className="inline-flex items-center gap-1">
                          <FontAwesomeIcon icon={faShareNodes} className="text-blue-400" />
                          {wf.destinations?.length ?? 0}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>

                <div className="flex items-center justify-between pt-1 text-[11px] font-mono text-muted">
                  <span>Total feeds to sync: {feedCount}</span>
                  <span>Exported: {parsedPackage.exportedAt ? new Date(parsedPackage.exportedAt).toLocaleDateString() : "Unknown"}</span>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </Modal>
  );
};
