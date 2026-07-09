import { useState, useEffect } from "react";
import { type AccountDto, listAccounts, saveAccount, deleteAccount } from "../../api/accounts";

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

        <form onSubmit={handleSave} className="space-y-4 max-w-sm">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Platform
            </label>
            <select
              value={platform}
              onChange={(e) => setPlatform(e.target.value)}
              disabled={true}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            >
              <option value="Bluesky">Bluesky</option>
            </select>
          </div>
          
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
