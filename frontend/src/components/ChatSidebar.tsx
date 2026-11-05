import { useEffect, useRef, useState } from "react";
import { sendChatMessage, type ChatMessage } from "@/lib/api";
import type { BoardData } from "@/lib/kanban";

export interface ChatSidebarProps {
  isOpen: boolean;
  onClose: () => void;
  onBoardUpdate: (board: BoardData) => void;
}

interface DisplayMessage extends ChatMessage {
  id: string;
  hasBoardUpdate?: boolean;
}

const SUGGESTIONS = [
  "Add a card 'Prepare demo slides' to Backlog",
  "Move 'Align roadmap themes' to In Progress",
  "What cards do I have in Progress?",
];

export const ChatSidebar = ({
  isOpen,
  onClose,
  onBoardUpdate,
}: ChatSidebarProps) => {
  const [messages, setMessages] = useState<DisplayMessage[]>([]);
  const [input, setInput] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen) {
      messagesEndRef.current?.scrollIntoView?.({ behavior: "smooth" });
    }
  }, [messages, isSending, isOpen]);

  const handleSend = async (textToSend: string) => {
    const trimmed = textToSend.trim();
    if (!trimmed || isSending) {
      return;
    }

    const userMsg: DisplayMessage = {
      id: `msg-${Date.now()}-user`,
      role: "user",
      content: trimmed,
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setIsSending(true);
    setError(null);

    // Prepare history payload (only role and content)
    const historyPayload: ChatMessage[] = messages.map((m) => ({
      role: m.role,
      content: m.content,
    }));

    try {
      const response = await sendChatMessage({
        message: trimmed,
        history: historyPayload,
      });

      const assistantMsg: DisplayMessage = {
        id: `msg-${Date.now()}-assistant`,
        role: "assistant",
        content: response.reply,
        hasBoardUpdate: !!response.board,
      };

      setMessages((prev) => [...prev, assistantMsg]);

      if (response.board) {
        onBoardUpdate(response.board);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to contact AI");
    } finally {
      setIsSending(false);
    }
  };

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    handleSend(input);
  };

  if (!isOpen) {
    return null;
  }

  return (
    <aside
      aria-label="AI Chat Sidebar"
      className="fixed inset-y-0 right-0 z-50 flex w-full max-w-[420px] flex-col border-l border-[var(--stroke)] bg-white/95 shadow-[-10px_0_30px_rgba(3,33,71,0.08)] backdrop-blur transition-all duration-300 sm:w-[420px]"
    >
      {/* Sidebar Header */}
      <div className="flex items-center justify-between border-b border-[var(--stroke)] p-6">
        <div>
          <div className="flex items-center gap-2">
            <span className="h-2 w-2 rounded-full bg-[var(--accent-yellow)]" />
            <span className="text-[10px] font-semibold uppercase tracking-[0.25em] text-[var(--gray-text)]">
              AI Assistant
            </span>
          </div>
          <h2 className="mt-1 font-display text-xl font-semibold text-[var(--navy-dark)]">
            Kanban Copilot
          </h2>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close AI Assistant"
          className="flex h-9 w-9 items-center justify-center rounded-xl border border-[var(--stroke)] text-[var(--gray-text)] transition hover:bg-[var(--surface)] hover:text-[var(--navy-dark)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]"
        >
          ✕
        </button>
      </div>

      {/* Messages Thread */}
      <div className="flex-1 overflow-y-auto p-6">
        {messages.length === 0 ? (
          <div className="flex flex-col gap-4 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--surface)] text-[var(--primary-blue)] font-display text-lg font-bold border border-[var(--stroke)]">
              AI
            </div>
            <div>
              <p className="font-display font-semibold text-[var(--navy-dark)]">
                Ask me to manage your board
              </p>
              <p className="mt-1 text-xs text-[var(--gray-text)]">
                I can create cards, move them between stages, edit descriptions, or summarize progress.
              </p>
            </div>

            <div className="mt-4 flex flex-col gap-2 text-left">
              <span className="text-[10px] font-semibold uppercase tracking-wider text-[var(--gray-text)]">
                Suggested prompts:
              </span>
              {SUGGESTIONS.map((suggestion) => (
                <button
                  key={suggestion}
                  type="button"
                  onClick={() => handleSend(suggestion)}
                  className="rounded-xl border border-[var(--stroke)] bg-[var(--surface)] p-3 text-left text-xs text-[var(--navy-dark)] transition hover:border-[var(--primary-blue)] hover:bg-white focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]/20"
                >
                  {suggestion}
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            {messages.map((msg) => (
              <div
                key={msg.id}
                className={`flex flex-col ${
                  msg.role === "user" ? "items-end" : "items-start"
                }`}
              >
                <div
                  className={`max-w-[85%] rounded-2xl px-4 py-3 text-sm ${
                    msg.role === "user"
                      ? "bg-[var(--primary-blue)] text-white shadow-sm"
                      : "border border-[var(--stroke)] bg-[var(--surface)] text-[var(--navy-dark)] shadow-sm"
                  }`}
                >
                  <p className="whitespace-pre-wrap leading-relaxed">
                    {msg.content}
                  </p>
                  {msg.hasBoardUpdate ? (
                    <div className="mt-2 flex items-center gap-1.5 border-t border-black/5 pt-1.5 text-[10px] font-semibold uppercase tracking-wider text-[var(--secondary-purple)]">
                      <span className="h-1.5 w-1.5 rounded-full bg-[var(--accent-yellow)]" />
                      Board updated
                    </div>
                  ) : null}
                </div>
              </div>
            ))}

            {isSending ? (
              <div className="flex flex-col items-start">
                <div
                  aria-live="polite"
                  className="flex items-center gap-2 rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-4 py-3 text-xs text-[var(--gray-text)] shadow-sm"
                >
                  <span className="h-2 w-2 animate-ping rounded-full bg-[var(--primary-blue)]" />
                  <span>Thinking...</span>
                </div>
              </div>
            ) : null}

            {error ? (
              <div
                role="alert"
                className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-700"
              >
                {error}
              </div>
            ) : null}

            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input Form */}
      <form
        onSubmit={handleSubmit}
        className="border-t border-[var(--stroke)] bg-white/80 p-4"
      >
        <div className="flex gap-2">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            disabled={isSending}
            aria-label="Message to AI"
            placeholder="Ask AI to update cards or answer questions..."
            className="flex-1 rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-4 py-3 text-sm text-[var(--navy-dark)] transition focus:border-[var(--primary-blue)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]/20 disabled:opacity-60"
          />
          <button
            type="submit"
            disabled={isSending || !input.trim()}
            className="rounded-2xl bg-[var(--secondary-purple)] px-5 py-3 text-xs font-semibold uppercase tracking-wider text-white shadow-sm transition hover:opacity-95 focus:outline-none focus:ring-2 focus:ring-[var(--secondary-purple)]/40 disabled:opacity-50"
          >
            Send
          </button>
        </div>
      </form>
    </aside>
  );
};
