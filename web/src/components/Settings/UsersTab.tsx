import { useEffect, useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faUsers, faKey } from "@fortawesome/free-solid-svg-icons";
import { listUsers, createUser, updateUser, resetUserPassword, type UserDto } from "../../api/auth";
import { Modal } from "../common/Modal";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsEmptyState } from "./common/SettingsEmptyState";
import { SettingsAlert } from "./common/SettingsAlert";

export function UsersTab() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  // Add User Modal State
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newUsername, setNewUsername] = useState("");
  const [newEmail, setNewEmail] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newRole, setNewRole] = useState("User");
  const [adding, setAdding] = useState(false);

  // Reset Password Modal State
  const [resettingUserId, setResettingUserId] = useState<string | null>(null);
  const [resetPasswordVal, setResetPasswordVal] = useState("");
  const [resetting, setResetting] = useState(false);

  const loadUsers = async () => {
    setLoading(true);
    try {
      const data = await listUsers();
      setUsers(data);
    } catch (err: any) {
      setError(err.message || "Failed to load users");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadUsers();
  }, []);

  const handleOpenAdd = () => {
    setNewUsername("");
    setNewEmail("");
    setNewPassword("");
    setNewRole("User");
    setModalError(null);
    setIsAddModalOpen(true);
  };

  const handleCloseAdd = () => {
    if (adding) return;
    setIsAddModalOpen(false);
    setModalError(null);
  };

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUsername || !newEmail || !newPassword) return;

    setAdding(true);
    setModalError(null);
    try {
      await createUser({
        username: newUsername,
        email: newEmail,
        password: newPassword,
        role: newRole,
      });
      setNewUsername("");
      setNewEmail("");
      setNewPassword("");
      setNewRole("User");
      setIsAddModalOpen(false);
      await loadUsers();
    } catch (err: any) {
      setModalError(err.message || "Failed to create user");
    } finally {
      setAdding(false);
    }
  };

  const handleToggleActive = async (user: UserDto) => {
    try {
      await updateUser(user.id, { isActive: !user.isActive });
      await loadUsers();
    } catch (err: any) {
      setError(err.message || "Failed to toggle status");
    }
  };

  const handleOpenResetPassword = (userId: string) => {
    setResettingUserId(userId);
    setResetPasswordVal("");
    setModalError(null);
  };

  const handleCloseResetPassword = () => {
    if (resetting) return;
    setResettingUserId(null);
    setResetPasswordVal("");
    setModalError(null);
  };

  const handleResetPasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resettingUserId || !resetPasswordVal) return;

    setResetting(true);
    setModalError(null);
    try {
      await resetUserPassword(resettingUserId, { newPassword: resetPasswordVal });
      setResetPasswordVal("");
      setResettingUserId(null);
    } catch (err: any) {
      setModalError(err.message || "Failed to reset password");
    } finally {
      setResetting(false);
    }
  };

  const resettingUser = users.find((u) => u.id === resettingUserId);

  return (
    <div className="space-y-6 font-sans">
      {error && !isAddModalOpen && !resettingUserId && (
        <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />
      )}

      <SettingsSection
        title="User Accounts"
        description="Manage registered user accounts, system roles, and password resets."
        action={
          <button
            type="button"
            onClick={handleOpenAdd}
            className="inline-flex items-center gap-1.5 bg-accent text-bg font-medium text-xs py-1.5 px-3.5 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>Add User</span>
          </button>
        }
      >
        {loading && users.length === 0 ? (
          <div className="text-xs font-mono text-muted text-center py-6">
            Loading accounts...
          </div>
        ) : users.length === 0 ? (
          <SettingsEmptyState
            icon={faUsers}
            title="No user accounts registered"
            description="Create an account to allow team members to access the workspace."
            actionLabel="+ Add User"
            onAction={handleOpenAdd}
          />
        ) : (
          <div className="border border-border rounded-lg overflow-hidden">
            <table className="w-full border-collapse text-left font-mono text-xs text-foreground">
              <thead>
                <tr className="bg-panel/40 border-b border-border text-muted">
                  <th className="p-3.5 uppercase font-semibold text-[11px]">username</th>
                  <th className="p-3.5 uppercase font-semibold text-[11px]">email</th>
                  <th className="p-3.5 uppercase font-semibold text-[11px]">role</th>
                  <th className="p-3.5 uppercase font-semibold text-[11px]">status</th>
                  <th className="p-3.5 uppercase font-semibold text-[11px] text-right">actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {users.map((u) => (
                  <tr key={u.id} className="hover:bg-panel/20 transition-colors">
                    <td className="p-3.5 font-sans text-sm font-medium">{u.username}</td>
                    <td className="p-3.5 text-muted">{u.email}</td>
                    <td className="p-3.5">
                      <span
                        className={`px-2 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider ${
                          u.role === "Admin"
                            ? "bg-accent/20 text-accent border border-accent/30"
                            : "bg-muted/20 text-muted border border-border"
                        }`}
                      >
                        {u.role}
                      </span>
                    </td>
                    <td className="p-3.5">
                      <button
                        onClick={() => handleToggleActive(u)}
                        title="Click to toggle user status"
                        className={`px-2 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider transition-colors ${
                          u.isActive
                            ? "bg-green-500/20 text-green-400 hover:bg-green-500/30 border border-green-500/30"
                            : "bg-red-500/20 text-red-400 hover:bg-red-500/30 border border-red-500/30"
                        }`}
                      >
                        {u.isActive ? "active" : "inactive"}
                      </button>
                    </td>
                    <td className="p-3.5 text-right">
                      <button
                        onClick={() => handleOpenResetPassword(u.id)}
                        className="text-xs font-mono text-accent hover:underline uppercase tracking-wider inline-flex items-center gap-1"
                      >
                        <FontAwesomeIcon icon={faKey} className="w-3 h-3" />
                        <span>reset pass</span>
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SettingsSection>

      {/* Create User Modal */}
      <Modal
        isOpen={isAddModalOpen}
        onClose={handleCloseAdd}
        title="Create User Account"
        description="Add a new user credential to grant access to social-worker."
        maxWidth="md"
      >
        {modalError && (
          <SettingsAlert type="error" message={modalError} onDismiss={() => setModalError(null)} />
        )}

        <form onSubmit={handleCreate} className="space-y-4">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Username
            </label>
            <input
              type="text"
              value={newUsername}
              onChange={(e) => setNewUsername(e.target.value)}
              disabled={adding}
              required
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Email
            </label>
            <input
              type="email"
              value={newEmail}
              onChange={(e) => setNewEmail(e.target.value)}
              disabled={adding}
              required
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Password
            </label>
            <input
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              disabled={adding}
              required
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Role
            </label>
            <select
              value={newRole}
              onChange={(e) => setNewRole(e.target.value)}
              disabled={adding}
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            >
              <option value="User">User</option>
              <option value="Admin">Admin</option>
            </select>
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-border">
            <button
              type="button"
              onClick={handleCloseAdd}
              disabled={adding}
              className="border border-border text-foreground hover:bg-border/20 text-xs py-2 px-4 rounded-lg transition-colors font-mono uppercase tracking-wider"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={adding || !newUsername || !newEmail || !newPassword}
              className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
            >
              {adding ? "Creating..." : "Create Account"}
            </button>
          </div>
        </form>
      </Modal>

      {/* Reset Password Modal */}
      <Modal
        isOpen={Boolean(resettingUserId)}
        onClose={handleCloseResetPassword}
        title={`Reset Password: ${resettingUser?.username || "User"}`}
        description="Set a new password for this user account."
        maxWidth="sm"
      >
        {modalError && (
          <SettingsAlert type="error" message={modalError} onDismiss={() => setModalError(null)} />
        )}

        <form onSubmit={handleResetPasswordSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              New Password
            </label>
            <input
              type="password"
              value={resetPasswordVal}
              onChange={(e) => setResetPasswordVal(e.target.value)}
              disabled={resetting}
              required
              autoFocus
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-border">
            <button
              type="button"
              onClick={handleCloseResetPassword}
              disabled={resetting}
              className="border border-border text-foreground hover:bg-border/20 text-xs py-2 px-4 rounded-lg font-mono uppercase tracking-wider"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={resetting || !resetPasswordVal}
              className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
            >
              {resetting ? "Saving..." : "Save Password"}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
