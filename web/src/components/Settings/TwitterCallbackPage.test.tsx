import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { TwitterCallbackPage } from "./TwitterCallbackPage";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/accounts", () => ({
  submitTwitterCallback: vi.fn(),
}));

const mockSubmitTwitterCallback = accountsApi.submitTwitterCallback as Mock;

function renderPage(search = "") {
  return render(
    <MemoryRouter initialEntries={[`/oauth/callback/twitter${search}`]}>
      <TwitterCallbackPage />
    </MemoryRouter>
  );
}

describe("TwitterCallbackPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders error when error param is present in URL", async () => {
    renderPage("?error=access_denied&error_description=User%20denied%20access");

    expect(screen.getByTestId("twitter-callback-page")).toBeInTheDocument();
    expect(screen.getByText("Connection Failed")).toBeInTheDocument();
    expect(screen.getByText(/User denied access/i)).toBeInTheDocument();
    expect(mockSubmitTwitterCallback).not.toHaveBeenCalled();
  });

  it("renders error when code is missing", async () => {
    renderPage("?state=xyz");

    expect(screen.getByTestId("twitter-callback-page")).toBeInTheDocument();
    expect(screen.getByText("Connection Failed")).toBeInTheDocument();
    expect(screen.getByText(/No authorization code found/i)).toBeInTheDocument();
    expect(mockSubmitTwitterCallback).not.toHaveBeenCalled();
  });

  it("renders error when state is missing", async () => {
    renderPage("?code=abc");

    expect(screen.getByTestId("twitter-callback-page")).toBeInTheDocument();
    expect(screen.getByText("Connection Failed")).toBeInTheDocument();
    expect(screen.getByText(/No OAuth state parameter found/i)).toBeInTheDocument();
    expect(mockSubmitTwitterCallback).not.toHaveBeenCalled();
  });

  it("exchanges code and state successfully", async () => {
    mockSubmitTwitterCallback.mockResolvedValueOnce({
      externalId: "12345",
      username: "alice_in_x",
      tokenExpiresAt: "2026-10-09T00:00:00Z",
    });

    renderPage("?code=good_code&state=good_state");

    await waitFor(() => {
      expect(mockSubmitTwitterCallback).toHaveBeenCalledWith("good_code", "good_state");
      expect(screen.getByText("Connected!")).toBeInTheDocument();
      expect(screen.getByText("@alice_in_x")).toBeInTheDocument();
    });
  });

  it("handles exchange failure gracefully", async () => {
    mockSubmitTwitterCallback.mockRejectedValueOnce(new Error("Twitter token expired or invalid"));

    renderPage("?code=bad_code&state=bad_state");

    await waitFor(() => {
      expect(screen.getByText("Connection Failed")).toBeInTheDocument();
      expect(screen.getByText("Twitter token expired or invalid")).toBeInTheDocument();
    });
  });
});
