import { vi } from "vitest";
import {
  fetchKanbanBoard,
  loginUser,
  sendChatMessage,
  updateKanbanBoard,
} from "./api";
import { initialData } from "./kanban";

describe("api client", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("loginUser", () => {
    it("returns response on successful login", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: true,
        json: async () => ({ username: "user", status: "ok" }),
      } as Response);

      const res = await loginUser({ username: "user", password: "password" });
      expect(res).toEqual({ username: "user", status: "ok" });
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/auth/login",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ username: "user", password: "password" }),
        })
      );
    });

    it("throws error with detail message on failed login", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: false,
        json: async () => ({ detail: "Invalid username or password" }),
      } as Response);

      await expect(
        loginUser({ username: "user", password: "wrong" })
      ).rejects.toThrow("Invalid username or password");
    });
  });

  describe("fetchKanbanBoard", () => {
    it("fetches board data successfully", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: true,
        json: async () => initialData,
      } as Response);

      const data = await fetchKanbanBoard();
      expect(data).toEqual(initialData);
      expect(global.fetch).toHaveBeenCalledWith("/api/kanban");
    });

    it("throws error when fetching fails", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: false,
        json: async () => ({ detail: "Database error" }),
      } as Response);

      await expect(fetchKanbanBoard()).rejects.toThrow("Database error");
    });
  });

  describe("updateKanbanBoard", () => {
    it("persists board data successfully via PUT", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: true,
        json: async () => initialData,
      } as Response);

      const data = await updateKanbanBoard(initialData);
      expect(data).toEqual(initialData);
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/kanban",
        expect.objectContaining({
          method: "PUT",
          body: JSON.stringify(initialData),
        })
      );
    });

    it("throws error when update fails", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: false,
        json: async () => ({ detail: "Validation failed" }),
      } as Response);

      await expect(updateKanbanBoard(initialData)).rejects.toThrow(
        "Validation failed"
      );
    });
  });

  describe("sendChatMessage", () => {
    it("sends chat message and returns response", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: true,
        json: async () => ({ reply: "Hello from AI", board: null }),
      } as Response);

      const res = await sendChatMessage({
        message: "Hi AI",
        history: [{ role: "user", content: "Hi AI" }],
      });
      expect(res).toEqual({ reply: "Hello from AI", board: null });
      expect(global.fetch).toHaveBeenCalledWith(
        "/api/chat",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            message: "Hi AI",
            history: [{ role: "user", content: "Hi AI" }],
          }),
        })
      );
    });

    it("throws error on failure", async () => {
      vi.spyOn(global, "fetch").mockResolvedValueOnce({
        ok: false,
        json: async () => ({ detail: "AI error" }),
      } as Response);

      await expect(sendChatMessage({ message: "test" })).rejects.toThrow(
        "AI error"
      );
    });
  });
});

