import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { AccountTab } from "./AccountTab";
import { useAuthStore } from "../../store/authStore";

vi.mock("../../api/auth", () => ({
  changePassword: vi.fn(),
  setPreferredProvider: vi.fn(),
}));
vi.mock("../../api/providers", () => ({
  listAvailableProviders: vi.fn().mockResolvedValue([]),
}));

import { changePassword } from "../../api/auth";
const mockChangePassword = changePassword as Mock;

const mockUser = { id: "1", username: "alice", email: "alice@example.com", role: "Admin", preferredProviderId: null };

describe("AccountTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAuthStore.setState({ user: mockUser, updatePreferredProviderId: vi.fn() } as any);
  });

  it("displays user profile information", () => {
    render(<AccountTab />);
    expect(screen.getByText("alice")).toBeInTheDocument();
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
    expect(screen.getByText("Admin")).toBeInTheDocument();
  });

  it("shows validation error when submitting empty password form", async () => {
    render(<AccountTab />);
    fireEvent.click(screen.getByRole("button", { name: /update password/i }));
    expect(await screen.findByText("Please fill in all fields")).toBeInTheDocument();
    expect(mockChangePassword).not.toHaveBeenCalled();
  });

  it("shows error when new passwords do not match", async () => {
    const { container } = render(<AccountTab />);
    const [currentInput, newInput, confirmInput] = Array.from(
      container.querySelectorAll<HTMLInputElement>('input[type="password"]')
    );

    fireEvent.change(currentInput, { target: { value: "old" } });
    fireEvent.change(newInput, { target: { value: "new1" } });
    fireEvent.change(confirmInput, { target: { value: "new2" } });
    fireEvent.click(screen.getByRole("button", { name: /update password/i }));

    expect(await screen.findByText("New passwords do not match")).toBeInTheDocument();
  });

  it("calls changePassword and shows success on valid submission", async () => {
    mockChangePassword.mockResolvedValueOnce(undefined);
    const { container } = render(<AccountTab />);
    const [currentInput, newInput, confirmInput] = Array.from(
      container.querySelectorAll<HTMLInputElement>('input[type="password"]')
    );

    fireEvent.change(currentInput, { target: { value: "old" } });
    fireEvent.change(newInput, { target: { value: "newpw" } });
    fireEvent.change(confirmInput, { target: { value: "newpw" } });
    fireEvent.click(screen.getByRole("button", { name: /update password/i }));

    await waitFor(() => expect(mockChangePassword).toHaveBeenCalledWith("old", "newpw"));
    expect(await screen.findByText("Password updated successfully")).toBeInTheDocument();
  });

  it("shows error message when changePassword fails", async () => {
    mockChangePassword.mockRejectedValueOnce(new Error("Wrong password"));
    const { container } = render(<AccountTab />);
    const [currentInput, newInput, confirmInput] = Array.from(
      container.querySelectorAll<HTMLInputElement>('input[type="password"]')
    );

    fireEvent.change(currentInput, { target: { value: "bad" } });
    fireEvent.change(newInput, { target: { value: "newpw" } });
    fireEvent.change(confirmInput, { target: { value: "newpw" } });
    fireEvent.click(screen.getByRole("button", { name: /update password/i }));

    expect(await screen.findByText("Wrong password")).toBeInTheDocument();
  });
});
