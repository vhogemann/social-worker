import { useState, useEffect } from "react";
import { useAuthStore } from "../../store/authStore";
import { changePassword, setPreferredProvider } from "../../api/auth";
import { listAvailableProviders, type AvailableProviderDto } from "../../api/providers";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsAlert } from "./common/SettingsAlert";

export function AccountTab() {
  const user = useAuthStore((s) => s.user);
  const updatePreferredProviderId = useAuthStore((s) => s.updatePreferredProviderId);
  const [providers, setProviders] = useState<AvailableProviderDto[]>([]);
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [providerError, setProviderError] = useState<string | null>(null);
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
      <SettingsSection
        title="User Profile"
        description="Your personal account identity and authorization role."
      >
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-sm font-mono">
          <div className="p-3 bg-bg/20 border border-border rounded-lg">
            <div className="text-[10px] text-muted uppercase tracking-wider mb-1">USERNAME</div>
            <div className="text-foreground font-sans font-medium">{user?.username}</div>
          </div>
          <div className="p-3 bg-bg/20 border border-border rounded-lg">
            <div className="text-[10px] text-muted uppercase tracking-wider mb-1">EMAIL</div>
            <div className="text-foreground truncate">{user?.email}</div>
          </div>
          <div className="p-3 bg-bg/20 border border-border rounded-lg">
            <div className="text-[10px] text-muted uppercase tracking-wider mb-1">ROLE</div>
            <div className="text-accent uppercase font-semibold">{user?.role}</div>
          </div>
        </div>
      </SettingsSection>

      <SettingsSection
        title="Preferred AI Provider"
        description="Select the AI model and provider to use for assistant chat sessions."
      >
        {providerError && (
          <SettingsAlert
            type="error"
            message={providerError}
            onDismiss={() => setProviderError(null)}
            className="mb-4"
          />
        )}
        <div className="max-w-md space-y-2">
          <select
            value={user?.preferredProviderId || ""}
            onChange={async (e) => {
              const val = e.target.value || null;
              setProviderError(null);
              try {
                await setPreferredProvider(val);
                updatePreferredProviderId(val);
              } catch (err: any) {
                setProviderError(err.message || "Failed to update preferred provider");
              }
            }}
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent font-sans"
          >
            <option value="">System Default</option>
            {providers.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.model})
              </option>
            ))}
          </select>
          <p className="text-xs text-muted font-sans leading-relaxed">
            Overrides the system default provider for your account during draft composition and chat.
          </p>
        </div>
      </SettingsSection>

      <SettingsSection
        title="Change Password"
        description="Update your credentials to maintain account security."
      >
        {error && (
          <SettingsAlert
            type="error"
            message={error}
            onDismiss={() => setError(null)}
            className="mb-4"
          />
        )}

        {success && (
          <SettingsAlert
            type="success"
            message={success}
            onDismiss={() => setSuccess(null)}
            className="mb-4"
          />
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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
          >
            {loading ? "updating..." : "update password"}
          </button>
        </form>
      </SettingsSection>
    </div>
  );
}
