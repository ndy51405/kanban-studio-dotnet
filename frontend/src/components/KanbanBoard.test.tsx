import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { KanbanBoard } from "@/components/KanbanBoard";
import { initialData } from "@/lib/kanban";

const getFirstColumn = () => screen.getAllByTestId(/column-/i)[0];

describe("KanbanBoard", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(global, "fetch").mockImplementation(async (url, init) => {
      const urlStr = typeof url === "string" ? url : url.toString();
      if (urlStr.includes("/api/kanban")) {
        if (init?.method === "PUT") {
          return {
            ok: true,
            json: async () => JSON.parse(init.body as string),
          } as Response;
        }
        return {
          ok: true,
          json: async () => initialData,
        } as Response;
      }
      return {
        ok: true,
        json: async () => ({}),
      } as Response;
    });
  });

  it("renders five columns after loading board data", async () => {
    render(<KanbanBoard />);
    expect(await screen.findAllByTestId(/column-/i)).toHaveLength(5);
    expect(global.fetch).toHaveBeenCalledWith("/api/kanban");
  });

  it("renames a column and persists changes to API", async () => {
    render(<KanbanBoard />);
    await screen.findAllByTestId(/column-/i);
    const column = getFirstColumn();
    const input = within(column).getByLabelText("Column title");
    await userEvent.clear(input);
    await userEvent.type(input, "New Name");
    expect(input).toHaveValue("New Name");

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/kanban",
      expect.objectContaining({
        method: "PUT",
        body: expect.stringContaining('"title":"New Name"'),
      })
    );
  });

  it("adds and removes a card and persists changes to API", async () => {
    render(<KanbanBoard />);
    await screen.findAllByTestId(/column-/i);
    const column = getFirstColumn();
    const addButton = within(column).getByRole("button", {
      name: /add a card/i,
    });
    await userEvent.click(addButton);

    const titleInput = within(column).getByPlaceholderText(/card title/i);
    await userEvent.type(titleInput, "New card");
    const detailsInput = within(column).getByPlaceholderText(/details/i);
    await userEvent.type(detailsInput, "Notes");

    await userEvent.click(within(column).getByRole("button", { name: /add card/i }));

    expect(within(column).getByText("New card")).toBeInTheDocument();
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/kanban",
      expect.objectContaining({
        method: "PUT",
        body: expect.stringContaining('"title":"New card"'),
      })
    );

    const deleteButton = within(column).getByRole("button", {
      name: /delete new card/i,
    });
    await userEvent.click(deleteButton);

    expect(within(column).queryByText("New card")).not.toBeInTheDocument();
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/kanban",
      expect.objectContaining({
        method: "PUT",
      })
    );
  });

  it("edits a card and persists changes to API", async () => {
    render(<KanbanBoard />);
    await screen.findAllByTestId(/column-/i);
    const card = screen.getByTestId("card-card-1");
    await userEvent.click(
      within(card).getByRole("button", { name: /edit align roadmap themes/i })
    );

    const titleInput = within(card).getByLabelText("Edit card title");
    await userEvent.clear(titleInput);
    await userEvent.type(titleInput, "Updated title");
    const detailsInput = within(card).getByLabelText("Edit card details");
    await userEvent.clear(detailsInput);
    await userEvent.type(detailsInput, "Updated details");
    await userEvent.click(within(card).getByRole("button", { name: /save/i }));

    expect(within(card).getByText("Updated title")).toBeInTheDocument();
    expect(within(card).getByText("Updated details")).toBeInTheDocument();
    expect(screen.queryByText("Align roadmap themes")).not.toBeInTheDocument();

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/kanban",
      expect.objectContaining({
        method: "PUT",
        body: expect.stringContaining('"title":"Updated title"'),
      })
    );
  });

  it("cancels editing a card without changes", async () => {
    render(<KanbanBoard />);
    await screen.findAllByTestId(/column-/i);
    const card = screen.getByTestId("card-card-1");
    await userEvent.click(
      within(card).getByRole("button", { name: /edit align roadmap themes/i })
    );
    const titleInput = within(card).getByLabelText("Edit card title");
    await userEvent.clear(titleInput);
    await userEvent.type(titleInput, "Discarded");
    await userEvent.click(within(card).getByRole("button", { name: /cancel/i }));

    expect(within(card).getByText("Align roadmap themes")).toBeInTheDocument();
    expect(screen.queryByText("Discarded")).not.toBeInTheDocument();
  });

  it("calls onLogout when clicking the Log out button", async () => {
    const handleLogout = vi.fn();
    render(<KanbanBoard onLogout={handleLogout} />);
    await screen.findAllByTestId(/column-/i);
    const logoutButton = screen.getByRole("button", { name: /log out/i });
    await userEvent.click(logoutButton);
    expect(handleLogout).toHaveBeenCalledTimes(1);
  });
});
