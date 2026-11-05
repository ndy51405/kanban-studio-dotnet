import { useState } from "react";
import { loginUser } from "@/lib/api";

interface LoginFormProps {
  onLogin: (username: string) => void;
}

export const LoginForm = ({ onLogin }: LoginFormProps) => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const response = await loginUser({
        username: username.trim(),
        password,
      });
      onLogin(response.username);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Invalid username or password");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden px-4 py-12">
      <div className="pointer-events-none absolute left-0 top-0 h-[420px] w-[420px] -translate-x-1/3 -translate-y-1/3 rounded-full bg-[radial-gradient(circle,_rgba(32,157,215,0.25)_0%,_rgba(32,157,215,0.05)_55%,_transparent_70%)]" />
      <div className="pointer-events-none absolute bottom-0 right-0 h-[520px] w-[520px] translate-x-1/4 translate-y-1/4 rounded-full bg-[radial-gradient(circle,_rgba(117,57,145,0.18)_0%,_rgba(117,57,145,0.05)_55%,_transparent_75%)]" />

      <div className="relative w-full max-w-md rounded-[32px] border border-[var(--stroke)] bg-white/80 p-8 shadow-[var(--shadow)] backdrop-blur sm:p-10">
        <div className="flex items-center gap-2">
          <span className="h-2 w-2 rounded-full bg-[var(--accent-yellow)]" />
          <p className="text-xs font-semibold uppercase tracking-[0.35em] text-[var(--gray-text)]">
            Kanban Studio
          </p>
        </div>

        <h1 className="mt-3 font-display text-3xl font-semibold text-[var(--navy-dark)]">
          Sign In
        </h1>
        <p className="mt-2 text-sm text-[var(--gray-text)]">
          Enter your credentials to access your project board.
        </p>

        {error ? (
          <div
            role="alert"
            className="mt-6 rounded-2xl border border-red-200 bg-red-50 p-4 text-xs font-medium text-red-700"
          >
            {error}
          </div>
        ) : null}

        <form onSubmit={handleSubmit} className="mt-6 flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label
              htmlFor="username"
              className="text-xs font-semibold uppercase tracking-wider text-[var(--navy-dark)]"
            >
              Username
            </label>
            <input
              id="username"
              name="username"
              type="text"
              required
              autoComplete="username"
              spellCheck={false}
              autoCapitalize="none"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              placeholder="user"
              className="w-full rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-4 py-3 text-sm text-[var(--navy-dark)] transition focus:border-[var(--primary-blue)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]/20"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label
              htmlFor="password"
              className="text-xs font-semibold uppercase tracking-wider text-[var(--navy-dark)]"
            >
              Password
            </label>
            <input
              id="password"
              name="password"
              type="password"
              required
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-4 py-3 text-sm text-[var(--navy-dark)] transition focus:border-[var(--primary-blue)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]/20"
            />
          </div>

          <button
            type="button"
            onClick={() => {
              setUsername("user");
              setPassword("password");
              setError(null);
            }}
            className="mt-2 flex w-full items-center justify-between rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-4 py-3 text-left text-xs text-[var(--gray-text)] transition hover:border-[var(--primary-blue)] hover:bg-white focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]/20"
          >
            <span>
              <span className="font-semibold text-[var(--primary-blue)]">Demo login:</span>{" "}
              username <code className="font-mono text-[var(--navy-dark)]">user</code> / password{" "}
              <code className="font-mono text-[var(--navy-dark)]">password</code>
            </span>
            <span className="rounded-lg border border-[var(--stroke)] bg-[var(--surface)] px-2 py-1 text-[10px] font-semibold uppercase tracking-wider text-[var(--primary-blue)]">
              Fill
            </span>
          </button>

          <button
            type="submit"
            disabled={loading}
            className="mt-2 w-full rounded-2xl bg-[var(--secondary-purple)] px-6 py-3.5 text-sm font-semibold text-white shadow-sm transition hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-[var(--secondary-purple)]/40 disabled:opacity-50"
          >
            {loading ? "Signing in..." : "Sign In"}
          </button>
        </form>
      </div>
    </div>
  );
};
