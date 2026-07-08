import { useState } from "react";
import { useAuthStore } from "../../store/authStore";

export function LoginPage() {
  const [emailOrUsername, setEmailOrUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const login = useAuthStore((s) => s.login);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!emailOrUsername || !password) {
      setError("Please fill in all fields");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await login(emailOrUsername, password);
    } catch (err: any) {
      setError(err.message || "Invalid credentials");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="h-screen w-screen flex items-center justify-center bg-bg font-sans">
      <div className="w-full max-w-md p-8 bg-panel border border-border rounded-lg shadow-2xl">
        <div className="flex flex-col items-center mb-8">
          <h1 className="text-xl font-mono tracking-wider uppercase text-accent mb-1">
            social-worker
          </h1>
          <p className="text-xs text-muted font-mono uppercase tracking-widest">
            sign in to continue
          </p>
        </div>

        {error && (
          <div className="mb-4 p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1.5">
              Email or Username
            </label>
            <input
              type="text"
              value={emailOrUsername}
              onChange={(e) => setEmailOrUsername(e.target.value)}
              disabled={loading}
              className="w-full bg-bg border border-border rounded px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
              placeholder="Email or username"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1.5">
              Password
            </label>
            <div className="relative">
              <input
                type={showPassword ? "text" : "password"}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={loading}
                className="w-full bg-bg border border-border rounded px-3 py-2 pr-10 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
                placeholder="••••••••"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                tabIndex={-1}
                className="absolute inset-y-0 right-0 pr-3 flex items-center text-xs font-mono text-muted hover:text-accent"
              >
                {showPassword ? "hide" : "show"}
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full mt-6 bg-accent text-bg font-medium text-sm py-2 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider flex items-center justify-center gap-2"
          >
            {loading ? (
              <>
                <span className="inline-block w-3 h-3 border-2 border-bg border-t-accent rounded-full animate-spin" />
                signing in...
              </>
            ) : (
              "sign in"
            )}
          </button>
        </form>
      </div>
    </div>
  );
}
