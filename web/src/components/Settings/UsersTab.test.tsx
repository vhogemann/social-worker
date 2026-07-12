import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { UsersTab } from "./UsersTab";

vi.mock("../../api/auth", () => ({
  listUsers: vi.fn(),
  createUser: vi.fn(),
  updateUser: vi.fn(),
  resetUserPassword: vi.fn(),
}));

import { listUsers, createUser, updateUser, resetUserPassword } from "../../api/auth";
const mockListUsers = listUsers as Mock;
const mockCreateUser = createUser as Mock;
const mockUpdateUser = updateUser as Mock;
const mockResetUserPassword = resetUserPassword as Mock;

const makeUser = (id = "u1", overrides = {}) => ({
  id,
  username: "alice",
  email: "alice@example.com",
  role: "Admin",
  isActive: true,
  ...overrides,
});

describe("UsersTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockListUsers.mockResolvedValue([]);
  });

  it("lists users loaded on mount", async () => {
    mockListUsers.mockResolvedValueOnce([makeUser()]);
    render(<UsersTab />);

    expect(await screen.findByText("alice")).toBeInTheDocument();
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
  });

  it("shows error when loading users fails", async () => {
    mockListUsers.mockRejectedValueOnce(new Error("Failed to load users"));
    render(<UsersTab />);

    expect(await screen.findByText("Failed to load users")).toBeInTheDocument();
  });

  it("shows add user form when clicking add user button", async () => {
    render(<UsersTab />);
    await waitFor(() => expect(mockListUsers).toHaveBeenCalled());

    fireEvent.click(screen.getByRole("button", { name: /\+ add user/i }));

    expect(screen.getAllByRole("textbox")[0]).toBeInTheDocument();
  });

  it("creates a user and refreshes list", async () => {
    const newUser = makeUser("u2", { username: "bob", email: "bob@example.com", role: "User" });
    mockListUsers
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([newUser]);
    mockCreateUser.mockResolvedValueOnce(newUser);
    const { container } = render(<UsersTab />);
    await waitFor(() => expect(mockListUsers).toHaveBeenCalled());

    fireEvent.click(screen.getByRole("button", { name: /\+ add user/i }));

    const textboxes = screen.getAllByRole("textbox");
    await userEvent.type(textboxes[0], "bob");
    await userEvent.type(textboxes[1], "bob@example.com");
    const passwordInput = container.querySelector('input[type="password"]');
    fireEvent.change(passwordInput!, { target: { value: "pw123" } });

    fireEvent.click(screen.getByRole("button", { name: /create account/i }));

    await waitFor(() => expect(mockCreateUser).toHaveBeenCalledWith(
      expect.objectContaining({ username: "bob", email: "bob@example.com", password: "pw123" })
    ));
    expect(await screen.findByText("bob")).toBeInTheDocument();
  });

  it("toggles user active status", async () => {
    mockListUsers
      .mockResolvedValueOnce([makeUser("u1", { isActive: true })])
      .mockResolvedValueOnce([makeUser("u1", { isActive: false })]);
    mockUpdateUser.mockResolvedValueOnce(undefined);
    render(<UsersTab />);

    await screen.findByText("alice");
    fireEvent.click(screen.getByRole("button", { name: "active" }));

    await waitFor(() => expect(mockUpdateUser).toHaveBeenCalledWith("u1", { isActive: false }));
  });

  it("shows reset password form and submits new password", async () => {
    mockListUsers.mockResolvedValueOnce([makeUser()]);
    mockResetUserPassword.mockResolvedValueOnce(undefined);
    const { container } = render(<UsersTab />);

    await screen.findByText("alice");
    fireEvent.click(screen.getByRole("button", { name: /reset pass/i }));

    const passwordInput = container.querySelector('input[type="password"]');
    fireEvent.change(passwordInput!, { target: { value: "newpass" } });
    fireEvent.click(screen.getByRole("button", { name: /save password/i }));

    await waitFor(() => expect(mockResetUserPassword).toHaveBeenCalledWith("u1", { newPassword: "newpass" }));
  });
});
