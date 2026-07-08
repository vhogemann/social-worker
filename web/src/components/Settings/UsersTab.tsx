import { useEffect, useState } from "react";
import { listUsers, createUser, updateUser, resetUserPassword, type UserDto } from "../../api/auth";

export function UsersTab() {
  const [users, setUsers] = useState<UserDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [showAddForm, setShowAddForm] = useState(false);
  const [newUsername, setNewUsername] = useState("");
  const [newEmail, setNewEmail] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newRole, setNewRole] = useState("User");
  const [adding, setAdding] = useState(false);

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

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUsername || !newEmail || !newPassword) return;

    setAdding(true);
    setError(null);
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
      setShowAddForm(false);
      await loadUsers();
    } catch (err: any) {
      setError(err.message || "Failed to create user");
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

  const handleResetPasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resettingUserId || !resetPasswordVal) return;

    setResetting(true);
    try {
      await resetUserPassword(resettingUserId, { newPassword: resetPasswordVal });
      setResetPasswordVal("");
      setResettingUserId(null);
    } catch (err: any) {
      setError(err.message || "Failed to reset password");
    } finally {
      setResetting(false);
    }
  };

  return (
    <div className="space-y-6 font-sans">
      <div className="flex items-center justify-between border-b border-border pb-3">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted">
          user accounts list
        </h3>
        <button
          onClick={() => setShowAddForm(!showAddForm)}
          className="text-xs font-mono text-accent hover:opacity-80"
        >
          {showAddForm ? "cancel" : "+ add user"}
        </button>
      </div>

      {error && (
        <div className="p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
          {error}
        </div>
      )}

      {showAddForm && (
        <form onSubmit={handleCreate} className="bg-bg/40 border border-border p-4 rounded space-y-4 max-w-sm">
          <h4 className="text-xs font-mono uppercase tracking-wider text-accent">
            new user account
          </h4>
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
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
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
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
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
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
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
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            >
              <option value="User">User</option>
              <option value="Admin">Admin</option>
            </select>
          </div>
          <button
            type="submit"
            disabled={adding}
            className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
          >
            {adding ? "creating..." : "create account"}
          </button>
        </form>
      )}

      {resettingUserId && (
        <form onSubmit={handleResetPasswordSubmit} className="bg-bg/40 border border-border p-4 rounded space-y-4 max-w-sm">
          <h4 className="text-xs font-mono uppercase tracking-wider text-accent">
            reset user password
          </h4>
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
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={resetting}
              className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
            >
              {resetting ? "saving..." : "save password"}
            </button>
            <button
              type="button"
              onClick={() => setResettingUserId(null)}
              disabled={resetting}
              className="border border-border text-foreground hover:bg-border/30 text-xs py-1.5 px-4 rounded font-mono uppercase tracking-wider"
            >
              cancel
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <div className="text-xs font-mono text-muted text-center py-6">
          loading accounts...
        </div>
      ) : (
        <div className="border border-border rounded overflow-hidden">
          <table className="w-full border-collapse text-left font-mono text-xs text-foreground">
            <thead>
              <tr className="bg-panel/40 border-b border-border text-muted">
                <th className="p-3 uppercase">username</th>
                <th className="p-3 uppercase">email</th>
                <th className="p-3 uppercase">role</th>
                <th className="p-3 uppercase">status</th>
                <th className="p-3 uppercase text-right">actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {users.map((u) => (
                <tr key={u.id} className="hover:bg-panel/20">
                  <td className="p-3 font-sans text-sm font-medium">{u.username}</td>
                  <td className="p-3 text-muted">{u.email}</td>
                  <td className="p-3">
                    <span className={`px-1.5 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider ${
                      u.role === "Admin" ? "bg-accent/20 text-accent" : "bg-muted/20 text-muted"
                    }`}>
                      {u.role}
                    </span>
                  </td>
                  <td className="p-3">
                    <button
                      onClick={() => handleToggleActive(u)}
                      className={`px-1.5 py-0.5 rounded text-[10px] uppercase font-bold tracking-wider ${
                        u.isActive ? "bg-green-500/20 text-green-400" : "bg-red-500/20 text-red-400"
                      }`}
                    >
                      {u.isActive ? "active" : "inactive"}
                    </button>
                  </td>
                  <td className="p-3 text-right space-x-3">
                    <button
                      onClick={() => setResettingUserId(u.id)}
                      className="text-accent hover:opacity-80"
                    >
                      reset pass
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
