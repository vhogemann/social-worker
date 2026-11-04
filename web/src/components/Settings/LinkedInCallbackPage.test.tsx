import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { LinkedInCallbackPage } from "./LinkedInCallbackPage";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/accounts", () => ({
  submitLinkedInCallback: vi.fn(),
}));

const mockSubmitLinkedInCallback = accountsApi.submitLinkedInCallback as Mock;

function renderPage(search = "") {
  return render(
    <MemoryRouter initialEntries={[`/oauth/callback/linkedin${search}`]}>
      <LinkedInCallbackPage />
    </MemoryRouter>
  );
}

describe("LinkedInCallbackPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders error when error param is present in URL", async () => {
    renderPage("?error=user_cancelled_login&error_description=User%20cancelled%20login");

    expect(screen.getByTestId("linkedin-callback-page")).toBeInTheDocument();
    expect(screen.getByText("Connection Failed")).toBeInTheDocument();
    expect(screen.getByText(/User cancelled login/i)).toBeInTheDocument();
    expect(mockSubmitLinkedInCallback).not.toHaveBeenCalled();
  });

  it("renders error when code is missing", async () => {
    renderPage("?state=xyz");

    expect(screen.getByTestId("linkedin-callback-page")).toBeInTheDocument();
    expect(screen.getByText("Connection Failed")).toBeInTheDocument();
    expect(screen.getByText(/No authorization code found/i)).toBeInTheDocument();
    expect(mockSubmitLinkedInCallback).not.toHaveBeenCalled();
  });

  it("exchanges code and state successfully for personal profile", async () => {
    mockSubmitLinkedInCallback.mockResolvedValueOnce({
      personalHandle: "urn:li:person:12345",
      organizationHandles: [],
    });

    renderPage("?code=good_code&state=good_state");

    await waitFor(() => {
      expect(mockSubmitLinkedInCallback).toHaveBeenCalledWith("good_code", "good_state");
      expect(screen.getByText("Connected!")).toBeInTheDocument();
      expect(screen.getByText(/Your LinkedIn personal profile was connected successfully/i)).toBeInTheDocument();
    });
  });

  it("exchanges code and state successfully for organization handles", async () => {
    mockSubmitLinkedInCallback.mockResolvedValueOnce({
      personalHandle: null,
      organizationHandles: ["urn:li:organization:67890"],
    });

    renderPage("?code=good_code&state=good_state");

    await waitFor(() => {
      expect(mockSubmitLinkedInCallback).toHaveBeenCalledWith("good_code", "good_state");
      expect(screen.getByText("Connected!")).toBeInTheDocument();
      expect(screen.getByText(/Connected 1 company page/i)).toBeInTheDocument();
      expect(screen.getByText("urn:li:organization:67890")).toBeInTheDocument();
    });
  });

  it("handles exchange failure gracefully", async () => {
    mockSubmitLinkedInCallback.mockRejectedValueOnce(new Error("LinkedIn auth code expired"));

    renderPage("?code=bad_code&state=bad_state");

    await waitFor(() => {
      expect(screen.getByText("Connection Failed")).toBeInTheDocument();
      expect(screen.getByText("LinkedIn auth code expired")).toBeInTheDocument();
    });
  });
});
