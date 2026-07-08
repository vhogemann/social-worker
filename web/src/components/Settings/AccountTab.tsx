import { useState, useEffect } from "react";
import { useAuthStore } from "../../store/authStore";
import { changePassword, setPreferredProvider } from "../../api/auth";
import { listAvailableProviders, type AvailableProviderDto } from "../../api/providers";

export function AccountTab() {
  const user = useAuthStore((s) => s.user);
  const updatePreferredProviderId = useAuthStore((s) => s.updatePreferredProviderId);
  const [providers, setProviders] = useState<AvailableProviderDto[]>([]);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    listAvailableProviders().then(setProviders).catch(console.error);
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!currentPassword || !newPassword || !confirmPassword) {
      setError("Please fill in all fields");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New passwords do not match");
      return;
    }

    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      await changePassword(currentPassword, newPassword);
      setSuccess("Password updated successfully");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err: any) {
      setError(err.message || "Failed to update password");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6 font-sans">
      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          user profile
        </h3>
        <div className="grid grid-cols-2 gap-4 text-sm font-mono">
          <div>
            <div className="text-xs text-muted mb-0.5">USERNAME</div>
            <div className="text-foreground">{user?.username}</div>
          </div>
          <div>
            <div className="text-xs text-muted mb-0.5">EMAIL</div>
            <div className="text-foreground">{user?.email}</div>
          </div>
          <div>
            <div className="text-xs text-muted mb-0.5">ROLE</div>
            <div className="text-accent uppercase">{user?.role}</div>
          </div>
        </div>
      </div>

      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          preferred ai provider
        </h3>
        <div className="max-w-sm space-y-2">
          <select
            value={user?.preferredProviderId || ""}
            onChange={async (e) => {
              const val = e.target.value || null;
              try {
                await setPreferredProvider(val);
                updatePreferredProviderId(val);
              } catch (err: any) {
                alert(err.message || "Failed to update preferred provider");
              }
            }}
            className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent font-sans"
          >
            <option value="">System Default</option>
            {providers.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.model})
              </option>
            ))}
          </select>
          <p className="text-xs text-muted font-mono leading-relaxed">
            Select the AI model and provider to use for assistant chat. Defaults to the system administrator's configured default provider.
          </p>
        </div>
      </div>

      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          change password
        </h3>

        {error && (
          <div className="mb-4 p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
            {error}
          </div>
        )}

        {success && (
          <div className="mb-4 p-3 bg-green-950/40 border border-green-800 text-green-400 text-xs rounded font-mono">
            {success}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4 max-w-sm">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Current Password
            </label>
            <input
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              disabled={loading}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              New Password
            </label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={loading}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Confirm New Password
            </label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              disabled={loading}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
          >
            {loading ? "updating..." : "update password"}
          </button>
        </form>
      </div>
    </div>
  );
}
