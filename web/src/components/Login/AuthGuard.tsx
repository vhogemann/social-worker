import { useEffect } from "react";
import { useAuthStore } from "../../store/authStore";
import { LoginPage } from "./LoginPage";

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const initialized = useAuthStore((s) => s.initialized);
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const initialize = useAuthStore((s) => s.initialize);

  useEffect(() => {
    initialize();
  }, [initialize]);

  if (!initialized) {
    return (
      <div className="h-screen w-screen flex flex-col items-center justify-center bg-bg gap-3 font-mono text-xs text-muted uppercase tracking-widest">
        <span className="inline-block w-6 h-6 border-2 border-muted border-t-accent rounded-full animate-spin" />
        loading session...
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginPage />;
  }

  return <>{children}</>;
}
