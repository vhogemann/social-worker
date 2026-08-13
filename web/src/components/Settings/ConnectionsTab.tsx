import { useState, useEffect } from "react";
import { type AccountDto, listAccounts, saveAccount, deleteAccount, getLinkedInAuthUrl } from "../../api/accounts";

export function ConnectionsTab() {
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const [platform, setPlatform] = useState("Bluesky");
  const [handle, setHandle] = useState("");
  const [appPassword, setAppPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchAccounts = async () => {
    try {
      setLoading(true);
      const data = await listAccounts();
      setAccounts(data);
    } catch (err: any) {
      setError(err.message || "Failed to load connections");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAccounts();
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!platform || !handle || !appPassword) return;

    try {
      setIsSubmitting(true);
      setError(null);
      await saveAccount({ platform, handle, appPassword });
      setHandle("");
      setAppPassword("");
      await fetchAccounts();
    } catch (err: any) {
      setError(err.message || "Failed to save connection");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLinkedInConnect = async () => {
    try {
      setIsSubmitting(true);
      setError(null);
      const data = await getLinkedInAuthUrl();
      if (data.authUrl) {
        window.location.href = data.authUrl;
      }
    } catch (err: any) {
      setError(err.message || "Failed to initiate LinkedIn connection.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to remove this connection?")) return;
    try {
      await deleteAccount(id);
      await fetchAccounts();
    } catch (err: any) {
      setError(err.message || "Failed to delete connection");
    }
  };

  return (
    <div className="space-y-6 font-sans">
      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          Add Connection
        </h3>
        
        {error && (
          <div className="mb-4 p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
            {error}
          </div>
        )}

        <div className="space-y-4 max-w-sm">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Platform
            </label>
            <select
              value={platform}
              onChange={(e) => {
                setPlatform(e.target.value);
                setHandle("");
                setAppPassword("");
              }}
              disabled={isSubmitting}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            >
              <option value="Bluesky">Bluesky</option>
              <option value="LinkedIn">LinkedIn</option>
            </select>
          </div>
          
          {platform === "LinkedIn" ? (
            <div className="pt-2">
              <button
                type="button"
                onClick={handleLinkedInConnect}
                disabled={isSubmitting}
                className="w-full inline-flex items-center justify-center bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-xs py-2 px-4 rounded transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider gap-2 shadow-sm"
              >
                <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" xmlns="http://www.w3.org/2000/svg">
                  <path d="M19 0h-14c-2.761 0-5 2.239-5 5v14c0 2.761 2.239 5 5 5h14c2.762 0 5-2.239 5-5v-14c0-2.761-2.238-5-5-5zm-11 19h-3v-11h3v11zm-1.5-12.268c-.966 0-1.75-.779-1.75-1.75s.784-1.75 1.75-1.75 1.75.779 1.75 1.75-.784 1.75-1.75 1.75zm13.5 12.268h-3v-5.604c0-3.368-4-3.113-4 0v5.604h-3v-11h3v1.765c1.396-2.586 7-2.777 7 2.476v6.759z"/>
                </svg>
                {isSubmitting ? "Redirecting..." : "Connect LinkedIn"}
              </button>
              <p className="mt-2 text-[10px] text-muted font-mono leading-normal">
                You will be redirected to LinkedIn to authorize the social-worker app.
              </p>
            </div>
          ) : (
            <form onSubmit={handleSave} className="space-y-4">
              <div>
                <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Handle / Username
                </label>
                <input
                  type="text"
                  value={handle}
                  onChange={(e) => setHandle(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="e.g. user.bsky.social"
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
                />
              </div>

              <div>
                <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  App Password
                </label>
                <input
                  type="password"
                  value={appPassword}
                  onChange={(e) => setAppPassword(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="••••••••••••"
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
                />
                <p className="mt-1 flex items-center gap-1 text-[10px] text-muted font-mono leading-none">
                  Always use an App Password, never your main account password.
                </p>
              </div>

              <button
                type="submit"
                disabled={isSubmitting || !handle || !appPassword}
                className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
              >
                {isSubmitting ? "Saving..." : "Save Connection"}
              </button>
            </form>
          )}
        </div>
      </div>

      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          Your Connections
        </h3>
        
        {loading ? (
          <div className="text-xs text-muted font-mono">Loading...</div>
        ) : accounts.length === 0 ? (
          <div className="text-xs text-muted font-mono">No connections configured yet.</div>
        ) : (
          <div className="space-y-2">
            {accounts.map(acc => (
              <div key={acc.id} className="flex items-center justify-between p-3 border border-border bg-bg/20 rounded">
                <div>
                  <div className="text-sm font-medium text-foreground">{acc.platform}</div>
                  <div className="text-xs text-muted font-mono mt-0.5">{acc.handle} • {acc.status}</div>
                </div>
                <button
                  onClick={() => handleDelete(acc.id)}
                  className="text-xs font-mono uppercase tracking-wider text-red-400 hover:text-red-300 px-3 py-1.5 border border-red-900/30 bg-red-950/20 rounded transition-colors"
                >
                  Remove
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
