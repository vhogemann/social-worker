import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { AuthGuard } from "./components/Login/AuthGuard";
import { ErrorBoundary } from "./components/common/ErrorBoundary";
import "./index.css";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { config } from "@fortawesome/fontawesome-svg-core";
config.autoAddCss = false;

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ErrorBoundary>
      <AuthGuard>
        <App />
      </AuthGuard>
    </ErrorBoundary>
  </React.StrictMode>
);
