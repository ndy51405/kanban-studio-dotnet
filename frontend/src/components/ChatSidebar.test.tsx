import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { ChatSidebar } from "@/components/ChatSidebar";
import { initialData } from "@/lib/kanban";

describe("ChatSidebar", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders nothing when isOpen is false", () => {
    const { container } = render(
      <ChatSidebar isOpen={false} onClose={vi.fn()} onBoardUpdate={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders header, suggestions, input, and close button when isOpen is true", () => {
    render(
      <ChatSidebar isOpen={true} onClose={vi.fn()} onBoardUpdate={vi.fn()} />
    );

    expect(screen.getByRole("heading", { name: "Kanban Copilot" })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Ask AI to update cards/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /send/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Close AI Assistant" })).toBeInTheDocument();
    expect(screen.getByText(/Suggested prompts:/i)).toBeInTheDocument();
  });

  it("calls onClose when clicking close button", async () => {
    const handleClose = vi.fn();
    render(
      <ChatSidebar isOpen={true} onClose={handleClose} onBoardUpdate={vi.fn()} />
    );

    const closeBtn = screen.getByRole("button", { name: "Close AI Assistant" });
    await userEvent.click(closeBtn);
    expect(handleClose).toHaveBeenCalledTimes(1);
  });

  it("sends a chat message and renders assistant reply without board update", async () => {
    vi.spyOn(global, "fetch").mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        reply: "You have 5 columns on your board.",
        board: null,
      }),
    } as Response);

    const handleBoardUpdate = vi.fn();
    render(
      <ChatSidebar
        isOpen={true}
        onClose={vi.fn()}
        onBoardUpdate={handleBoardUpdate}
      />
    );

    const input = screen.getByLabelText("Message to AI");
    await userEvent.type(input, "What columns do I have?");
    await userEvent.click(screen.getByRole("button", { name: /send/i }));

    // User message is displayed immediately
    expect(screen.getByText("What columns do I have?")).toBeInTheDocument();

    // Assistant reply is displayed
    expect(
      await screen.findByText("You have 5 columns on your board.")
    ).toBeInTheDocument();

    expect(handleBoardUpdate).not.toHaveBeenCalled();
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/chat",
      expect.objectContaining({
        method: "POST",
        body: expect.stringContaining('"message":"What columns do I have?"'),
      })
    );
  });

  it("sends a message that modifies the board, calls onBoardUpdate, and shows badge", async () => {
    const updatedBoard = {
      ...initialData,
      columns: [
        {
          ...initialData.columns[0],
          title: "New Ideas",
        },
        ...initialData.columns.slice(1),
      ],
    };

    vi.spyOn(global, "fetch").mockResolvedValueOnce({
      ok: true,
      json: async () => ({
        reply: "I have renamed the Backlog column to New Ideas.",
        board: updatedBoard,
      }),
    } as Response);

    const handleBoardUpdate = vi.fn();
    render(
      <ChatSidebar
        isOpen={true}
        onClose={vi.fn()}
        onBoardUpdate={handleBoardUpdate}
      />
    );

    const input = screen.getByLabelText("Message to AI");
    await userEvent.type(input, "Rename Backlog to New Ideas");
    await userEvent.click(screen.getByRole("button", { name: /send/i }));

    expect(
      await screen.findByText("I have renamed the Backlog column to New Ideas.")
    ).toBeInTheDocument();
    expect(screen.getByText(/Board updated/i)).toBeInTheDocument();
    expect(handleBoardUpdate).toHaveBeenCalledWith(updatedBoard);
  });

  it("displays error message when AI request fails", async () => {
    vi.spyOn(global, "fetch").mockResolvedValueOnce({
      ok: false,
      json: async () => ({ detail: "AI service unavailable" }),
    } as Response);

    render(
      <ChatSidebar isOpen={true} onClose={vi.fn()} onBoardUpdate={vi.fn()} />
    );

    const input = screen.getByLabelText("Message to AI");
    await userEvent.type(input, "Hello");
    await userEvent.click(screen.getByRole("button", { name: /send/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "AI service unavailable"
    );
  });
});

