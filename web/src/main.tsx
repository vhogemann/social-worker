import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { AuthGuard } from "./components/Login/AuthGuard";
import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AuthGuard>
      <App />
    </AuthGuard>
  </React.StrictMode>
);
