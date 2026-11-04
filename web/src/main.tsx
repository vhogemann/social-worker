import React from "react";
import ReactDOM from "react-dom/client";
import { StyledEngineProvider } from "@mui/material/styles";
import { ColorModeProvider } from "./theme/ColorModeContext";
import App from "./App";
import { AuthGuard } from "./components/Login/AuthGuard";
import { ErrorBoundary } from "./components/common/ErrorBoundary";
import "./index.css";
import "@fortawesome/fontawesome-svg-core/styles.css";
import { config } from "@fortawesome/fontawesome-svg-core";
config.autoAddCss = false;

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <StyledEngineProvider injectFirst>
      <ColorModeProvider>
        <ErrorBoundary>
          <AuthGuard>
            <App />
          </AuthGuard>
        </ErrorBoundary>
      </ColorModeProvider>
    </StyledEngineProvider>
  </React.StrictMode>
);
