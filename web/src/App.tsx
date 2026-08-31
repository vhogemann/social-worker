import React from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AppHeader } from "./components/Navigation/AppHeader";
import { WorkflowsPage } from "./components/WorkflowsPanel/WorkflowsPage";
import { WorkflowDetailPage } from "./components/WorkflowDetail/WorkflowDetailPage";
import { DraftsPage } from "./components/Drafts/DraftsPage";
import { ComposerPage } from "./components/Composer/ComposerPage";
import { TemplateStudioPage } from "./components/TemplateStudio/TemplateStudioPage";
import { TemplateEditorPage } from "./components/TemplateStudio/TemplateEditorPage";
import { SourcesLibraryPage } from "./components/SourcesLibrary/SourcesLibraryPage";
import { LogsPage } from "./components/Logs/LogsPage";
import { PrivacyPolicyPage } from "./components/Privacy/PrivacyPolicyPage";
import { SettingsPage } from "./components/Settings/SettingsPage";
import { LinkedInCallbackPage } from "./components/Settings/LinkedInCallbackPage";

export default function App() {
  return (
    <BrowserRouter>
      <div className="h-screen flex flex-col overflow-hidden">
        <AppHeader />
        <Routes>
          <Route path="/" element={<WorkflowsPage />} />
          <Route path="/workflows" element={<Navigate to="/" replace />} />
          <Route path="/workflows/:id" element={<WorkflowDetailPage />} />
          <Route path="/workflows/:workflowId/draft" element={<ComposerPage />} />
          <Route path="/workflows/:workflowId/draft/:draftId" element={<ComposerPage />} />
          <Route path="/workflows/:workflowId/draft/:draftId/variant/:variantId" element={<ComposerPage />} />
          <Route path="/drafts" element={<DraftsPage />} />
          <Route path="/composer" element={<ComposerPage />} />
          <Route path="/draft/:draftId" element={<ComposerPage />} />
          <Route path="/draft/:draftId/variant/:variantId" element={<ComposerPage />} />
          <Route path="/templates" element={<TemplateStudioPage />} />
          <Route path="/templates/editor" element={<TemplateEditorPage />} />
          <Route path="/templates/editor/:id" element={<TemplateEditorPage />} />
          <Route path="/sources" element={<SourcesLibraryPage />} />
          <Route path="/feeds" element={<Navigate to="/settings/feeds" replace />} />
          <Route path="/logs" element={<LogsPage />} />
          <Route path="/privacy" element={<PrivacyPolicyPage />} />
          <Route path="/settings" element={<Navigate to="/settings/account" replace />} />
          <Route path="/settings/:tab" element={<SettingsPage />} />
          <Route path="/oauth/callback/linkedin" element={<LinkedInCallbackPage />} />
        </Routes>
      </div>
    </BrowserRouter>
  );
}