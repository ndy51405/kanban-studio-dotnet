import { useEffect, useState } from "react";
import { KanbanBoard } from "@/components/KanbanBoard";
import { LoginForm } from "@/components/LoginForm";

export const AUTH_STORAGE_KEY = "kanban_user";

export default function App() {
  const [user, setUser] = useState<string | null>(null);

  useEffect(() => {
    const stored = window.localStorage.getItem(AUTH_STORAGE_KEY);
    if (stored) {
      setUser(stored);
    }
  }, []);

  const handleLogin = (username: string) => {
    window.localStorage.setItem(AUTH_STORAGE_KEY, username);
    setUser(username);
  };

  const handleLogout = () => {
    window.localStorage.removeItem(AUTH_STORAGE_KEY);
    setUser(null);
  };

  if (!user) {
    return <LoginForm onLogin={handleLogin} />;
  }

  return <KanbanBoard user={user} onLogout={handleLogout} />;
}
