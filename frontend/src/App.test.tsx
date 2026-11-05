import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import App, { AUTH_STORAGE_KEY } from "@/App";
import { initialData } from "@/lib/kanban";

describe("Home Page Authentication Flow", () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();

    vi.spyOn(global, "fetch").mockImplementation(async (url, init) => {
      const urlStr = typeof url === "string" ? url : url.toString();
      if (urlStr.includes("/api/auth/login")) {
        const body = JSON.parse(init?.body as string);
        if (body.username === "user" && body.password === "password") {
          return {
            ok: true,
            json: async () => ({ username: "user", status: "ok" }),
          } as Response;
        }
        return {
          ok: false,
          json: async () => ({ detail: "Invalid username or password" }),
        } as Response;
      }
      if (urlStr.includes("/api/kanban")) {
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

  it("renders the login form when unauthenticated", () => {
    render(<App />);
    expect(screen.getByRole("heading", { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /kanban studio/i, level: 1 })).not.toBeInTheDocument();
  });

  it("authenticates and transitions to the Kanban board upon successful login", async () => {
    render(<App />);

    await userEvent.type(screen.getByLabelText(/username/i), "user");
    await userEvent.type(screen.getByLabelText(/password/i), "password");
    await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

    expect(await screen.findAllByTestId(/column-/i)).toHaveLength(5);
    expect(screen.getByRole("heading", { name: "Kanban Studio" })).toBeInTheDocument();
    expect(window.localStorage.getItem(AUTH_STORAGE_KEY)).toBe("user");
  });

  it("displays error and remains on login form when credentials are invalid", async () => {
    render(<App />);

    await userEvent.type(screen.getByLabelText(/username/i), "admin");
    await userEvent.type(screen.getByLabelText(/password/i), "wrongpass");
    await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/invalid username or password/i);
    expect(screen.queryByRole("heading", { name: "Kanban Studio" })).not.toBeInTheDocument();
    expect(window.localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it("logs out and returns to the login form when clicking Log out", async () => {
    window.localStorage.setItem(AUTH_STORAGE_KEY, "user");
    render(<App />);

    const logoutButton = await screen.findByRole("button", { name: /log out/i });
    await userEvent.click(logoutButton);

    expect(await screen.findByRole("heading", { name: /sign in/i })).toBeInTheDocument();
    expect(window.localStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });

  it("automatically loads the board if session exists in localStorage", async () => {
    window.localStorage.setItem(AUTH_STORAGE_KEY, "user");
    render(<App />);

    expect(await screen.findAllByTestId(/column-/i)).toHaveLength(5);
    expect(screen.getByRole("heading", { name: "Kanban Studio" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /sign in/i })).not.toBeInTheDocument();
  });
});
