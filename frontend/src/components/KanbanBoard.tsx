import { useEffect, useMemo, useState } from "react";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  closestCorners,
  type DragEndEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { KanbanColumn } from "@/components/KanbanColumn";
import { KanbanCardPreview } from "@/components/KanbanCardPreview";
import { ChatSidebar } from "@/components/ChatSidebar";
import { createId, initialData, moveCard, type BoardData } from "@/lib/kanban";
import { fetchKanbanBoard, updateKanbanBoard } from "@/lib/api";

export interface KanbanBoardProps {
  user?: string;
  onLogout?: () => void;
  initialBoard?: BoardData;
}

export const KanbanBoard = ({
  user: _user,
  onLogout,
  initialBoard,
}: KanbanBoardProps = {}) => {
  const [board, setBoard] = useState<BoardData>(() => initialBoard || initialData);
  const [loading, setLoading] = useState(!initialBoard);
  const [error, setError] = useState<string | null>(null);
  const [activeCardId, setActiveCardId] = useState<string | null>(null);
  const [isChatOpen, setIsChatOpen] = useState(false);

  useEffect(() => {
    if (initialBoard) {
      return;
    }
    let isMounted = true;
    fetchKanbanBoard()
      .then((data) => {
        if (isMounted) {
          setBoard(data);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (isMounted) {
          console.error("Failed to fetch board:", err);
          setError("Failed to load latest board data. Using local state.");
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, [initialBoard]);

  const persistBoard = (newBoard: BoardData) => {
    updateKanbanBoard(newBoard).catch((err) => {
      console.error("Failed to persist board:", err);
      setError("Failed to save changes to the server.");
    });
  };

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 6 },
    })
  );

  const cardsById = useMemo(() => board.cards, [board.cards]);

  const handleDragStart = (event: DragStartEvent) => {
    setActiveCardId(event.active.id as string);
  };

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    setActiveCardId(null);

    if (!over || active.id === over.id) {
      return;
    }

    setBoard((prev) => {
      const updated = {
        ...prev,
        columns: moveCard(prev.columns, active.id as string, over.id as string),
      };
      persistBoard(updated);
      return updated;
    });
  };

  const handleRenameColumn = (columnId: string, title: string) => {
    setBoard((prev) => {
      const updated = {
        ...prev,
        columns: prev.columns.map((column) =>
          column.id === columnId ? { ...column, title } : column
        ),
      };
      persistBoard(updated);
      return updated;
    });
  };

  const handleAddCard = (columnId: string, title: string, details: string) => {
    const id = createId("card");
    setBoard((prev) => {
      const updated = {
        ...prev,
        cards: {
          ...prev.cards,
          [id]: { id, title, details: details || "No details yet." },
        },
        columns: prev.columns.map((column) =>
          column.id === columnId
            ? { ...column, cardIds: [...column.cardIds, id] }
            : column
        ),
      };
      persistBoard(updated);
      return updated;
    });
  };

  const handleDeleteCard = (columnId: string, cardId: string) => {
    setBoard((prev) => {
      const updated = {
        ...prev,
        cards: Object.fromEntries(
          Object.entries(prev.cards).filter(([id]) => id !== cardId)
        ),
        columns: prev.columns.map((column) =>
          column.id === columnId
            ? {
                ...column,
                cardIds: column.cardIds.filter((id) => id !== cardId),
              }
            : column
        ),
      };
      persistBoard(updated);
      return updated;
    });
  };

  const handleEditCard = (cardId: string, title: string, details: string) => {
    setBoard((prev) => {
      const updated = {
        ...prev,
        cards: {
          ...prev.cards,
          [cardId]: { id: cardId, title, details: details || "No details yet." },
        },
      };
      persistBoard(updated);
      return updated;
    });
  };

  const activeCard = activeCardId ? cardsById[activeCardId] : null;

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-[var(--surface)]">
        <div className="text-center">
          <h1 className="font-display text-2xl font-semibold text-[var(--navy-dark)]">
            Kanban Studio
          </h1>
          <p className="mt-2 text-xs font-semibold uppercase tracking-[0.25em] text-[var(--gray-text)]">
            Loading board...
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="relative overflow-hidden">
      <div className="pointer-events-none absolute left-0 top-0 h-[420px] w-[420px] -translate-x-1/3 -translate-y-1/3 rounded-full bg-[radial-gradient(circle,_rgba(32,157,215,0.25)_0%,_rgba(32,157,215,0.05)_55%,_transparent_70%)]" />
      <div className="pointer-events-none absolute bottom-0 right-0 h-[520px] w-[520px] translate-x-1/4 translate-y-1/4 rounded-full bg-[radial-gradient(circle,_rgba(117,57,145,0.18)_0%,_rgba(117,57,145,0.05)_55%,_transparent_75%)]" />

      <main className="relative mx-auto flex min-h-screen max-w-[1500px] flex-col gap-10 px-6 pb-16 pt-12">
        <header className="flex flex-col gap-6 rounded-[32px] border border-[var(--stroke)] bg-white/80 p-8 shadow-[var(--shadow)] backdrop-blur">
          {error ? (
            <div
              role="alert"
              className="flex items-center justify-between rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-xs font-medium text-red-700"
            >
              <span>{error}</span>
              <button
                type="button"
                onClick={() => setError(null)}
                className="ml-4 font-bold text-red-700 hover:opacity-70"
              >
                Dismiss
              </button>
            </div>
          ) : null}

          <div className="flex flex-wrap items-start justify-between gap-6">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.35em] text-[var(--gray-text)]">
                Single Board Kanban
              </p>
              <h1 className="mt-3 font-display text-4xl font-semibold text-[var(--navy-dark)]">
                Kanban Studio
              </h1>
              <p className="mt-3 max-w-xl text-sm leading-6 text-[var(--gray-text)]">
                Keep momentum visible. Rename columns, drag cards between stages,
                and capture quick notes without getting buried in settings.
              </p>
            </div>
            <div className="flex flex-col items-start gap-3 sm:flex-row sm:items-center">
              <div className="rounded-2xl border border-[var(--stroke)] bg-[var(--surface)] px-5 py-4">
                <p className="text-xs font-semibold uppercase tracking-[0.25em] text-[var(--gray-text)]">
                  Focus
                </p>
                <p className="mt-2 text-lg font-semibold text-[var(--primary-blue)]">
                  One board. Five columns. Zero clutter.
                </p>
              </div>
              <button
                type="button"
                onClick={() => setIsChatOpen((prev) => !prev)}
                className="flex items-center gap-2 rounded-2xl border border-[var(--stroke)] bg-white px-5 py-4 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--navy-dark)] shadow-sm transition hover:bg-[var(--surface)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]"
              >
                <span className="h-2 w-2 rounded-full bg-[var(--accent-yellow)]" />
                AI Assistant
              </button>
              {onLogout ? (
                <button
                  type="button"
                  onClick={onLogout}
                  className="rounded-2xl border border-[var(--stroke)] bg-white px-5 py-4 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--navy-dark)] shadow-sm transition hover:bg-[var(--surface)] focus:outline-none focus:ring-2 focus:ring-[var(--primary-blue)]"
                >
                  Log out
                </button>
              ) : null}
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-4">
            {board.columns.map((column) => (
              <div
                key={column.id}
                className="flex items-center gap-2 rounded-full border border-[var(--stroke)] px-4 py-2 text-xs font-semibold uppercase tracking-[0.2em] text-[var(--navy-dark)]"
              >
                <span className="h-2 w-2 rounded-full bg-[var(--accent-yellow)]" />
                {column.title}
              </div>
            ))}
          </div>
        </header>

        <DndContext
          sensors={sensors}
          collisionDetection={closestCorners}
          onDragStart={handleDragStart}
          onDragEnd={handleDragEnd}
        >
          <section className="grid gap-6 lg:grid-cols-5">
            {board.columns.map((column) => (
              <KanbanColumn
                key={column.id}
                column={column}
                cards={column.cardIds.map((cardId) => board.cards[cardId])}
                onRename={handleRenameColumn}
                onAddCard={handleAddCard}
                onDeleteCard={handleDeleteCard}
                onEditCard={handleEditCard}
              />
            ))}
          </section>
          <DragOverlay>
            {activeCard ? (
              <div className="w-[260px]">
                <KanbanCardPreview card={activeCard} />
              </div>
            ) : null}
          </DragOverlay>
        </DndContext>
      </main>

      <ChatSidebar
        isOpen={isChatOpen}
        onClose={() => setIsChatOpen(false)}
        onBoardUpdate={(newBoard) => setBoard(newBoard)}
      />
    </div>
  );
};
