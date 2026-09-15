import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { submitLinkedInCallback, type LinkedInCallbackResponse } from "../../api/accounts";

export function LinkedInCallbackPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<LinkedInCallbackResponse | null>(null);

  const code = searchParams.get("code");
  const state = searchParams.get("state");

  useEffect(() => {
    if (!code) {
      setStatus("error");
      setError("No authorization code found in redirect URL.");
      return;
    }

    let active = true;

    async function handleExchange() {
      try {
        const data = await submitLinkedInCallback(code!, state || undefined);
        if (!active) return;
        setResult(data);
        setStatus("success");
        setTimeout(() => {
          navigate("/settings?tab=connections", { replace: true });
        }, 2500);
      } catch (err: any) {
        if (!active) return;
        setStatus("error");
        setError(err.message || "Failed to link LinkedIn account.");
      }
    }

    handleExchange();

    return () => {
      active = false;
    };
  }, [code, state, navigate]);

  return (
    <div className="min-h-screen bg-zinc-50 dark:bg-zinc-950 flex flex-col items-center justify-center p-6 text-sans" data-testid="linkedin-callback-page">
      <div className="max-w-md w-full bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-2xl p-8 shadow-md text-center">
        {status === "loading" && (
          <div className="space-y-4">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-4 border-indigo-500 border-t-transparent"></div>
            <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Connecting to LinkedIn</h2>
            <p className="text-sm text-zinc-500 dark:text-zinc-400">Verifying authorization token and retrieving profile details...</p>
          </div>
        )}

        {status === "success" && (
          <div className="space-y-4">
            <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-green-100 dark:bg-green-950 text-green-600 dark:text-green-400">
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={2} stroke="currentColor" className="w-6 h-6">
                <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
              </svg>
            </div>
            <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Connected!</h2>
            <div className="text-sm text-zinc-500 dark:text-zinc-400 space-y-1">
              {result?.personalHandle ? (
                <p>Your LinkedIn personal profile was connected successfully.</p>
              ) : (
                <p>Your LinkedIn connection was established successfully.</p>
              )}
              {result && result.organizationHandles.length > 0 && (
                <p className="text-xs font-mono text-zinc-600 dark:text-zinc-300">
                  Connected {result.organizationHandles.length} company page(s):{" "}
                  <span className="font-semibold">{result.organizationHandles.join(", ")}</span>
                </p>
              )}
            </div>
            <p className="text-xs text-zinc-400 dark:text-zinc-500">Redirecting to settings...</p>
          </div>
        )}

        {status === "error" && (
          <div className="space-y-4">
            <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-red-100 dark:bg-red-950 text-red-600 dark:text-red-400">
              <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={2} stroke="currentColor" className="w-6 h-6">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
              </svg>
            </div>
            <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Connection Failed</h2>
            <p className="text-sm text-red-500 font-mono bg-red-50 dark:bg-red-950/20 p-3 rounded-lg border border-red-200 dark:border-red-900/50 break-words">{error}</p>
            <button
              onClick={() => navigate("/", { replace: true })}
              className="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold transition-colors"
            >
              Back to Dashboard
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export { LinkedInCallbackPage as LinkedInCallback };
