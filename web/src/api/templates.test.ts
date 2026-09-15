import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchTemplates,
  fetchTemplate,
  createTemplate,
  updateTemplate,
  deleteTemplate,
  generateTemplate,
  previewTemplate,
  previewTemplateWithImages,
  seedDefaultTemplates,
  restoreDefaultTemplates,
  type TemplateDto,
} from "./templates";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeTemplate = (id = "t1"): TemplateDto => ({
  id,
  userId: "u1",
  name: "Quote Card",
  description: "Minimal quote card",
  width: 1080,
  height: 1080,
  schema: "{\"name\":\"Quote Card\",\"slots\":[],\"layers\":[]}",
  thumbnailMediaAssetId: null,
  thumbnailUrl: "/api/templates/t1/render",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
});

describe("templates API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  describe("fetchTemplates", () => {
    it("returns template list without search query", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeTemplate()]), { status: 200 })
      );
      const result = await fetchTemplates();
      expect(result).toHaveLength(1);
      expect(result[0].name).toBe("Quote Card");
      expect(result[0].userId).toBe("u1");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/templates");
    });

    it("passes search query parameter", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeTemplate()]), { status: 200 })
      );
      await fetchTemplates("quote");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/templates?search=quote");
    });
  });

  describe("fetchTemplate", () => {
    it("fetches single template by id", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(makeTemplate("t1")), { status: 200 })
      );
      const result = await fetchTemplate("t1");
      expect(result.id).toBe("t1");
      expect(result.userId).toBe("u1");
      expect(mockApiFetch).toHaveBeenCalledWith("/api/templates/t1");
    });
  });

  describe("createTemplate", () => {
    it("sends POST and returns created template", async () => {
      const created = makeTemplate("t2");
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(created), { status: 201 })
      );

      const result = await createTemplate({
        name: "Quote Card",
        width: 1080,
        height: 1080,
        schema: "{}",
      });

      expect(result.id).toBe("t2");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("updateTemplate", () => {
    it("sends PUT and returns updated template", async () => {
      const updated = { ...makeTemplate("t1"), name: "Updated Name" };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(updated), { status: 200 })
      );

      const result = await updateTemplate("t1", {
        name: "Updated Name",
        width: 1080,
        height: 1080,
        schema: "{}",
      });

      expect(result.name).toBe("Updated Name");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/t1",
        expect.objectContaining({ method: "PUT" })
      );
    });
  });

  describe("deleteTemplate", () => {
    it("sends DELETE and resolves on success", async () => {
      mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
      await expect(deleteTemplate("t1")).resolves.toBeUndefined();
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/t1",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("generateTemplate", () => {
    it("sends POST to /generate and returns generated response", async () => {
      const generated = {
        name: "AI Generated Template",
        width: 1080,
        height: 1080,
        schema: "{}",
        previewBase64: "dGVzdA==",
      };
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify(generated), { status: 200 })
      );

      const result = await generateTemplate({ prompt: "Dark quote card" });
      expect(result.name).toBe("AI Generated Template");
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/generate",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("previewTemplate", () => {
    it("sends POST to /preview and returns blob", async () => {
      const blob = new Blob(["png bytes"], { type: "image/png" });
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: () => Promise.resolve(blob),
      });

      const result = await previewTemplate({ schema: "{}" });
      expect(result).toBeInstanceOf(Blob);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/preview",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("previewTemplateWithImages", () => {
    it("sends multipart POST to /preview-with-images and returns blob", async () => {
      const blob = new Blob(["png bytes"], { type: "image/png" });
      mockApiFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: () => Promise.resolve(blob),
      });

      const testFile = new File(["dummy"], "bg.png", { type: "image/png" });
      const result = await previewTemplateWithImages("{}", { quote: "Hi" }, { bg: testFile });

      expect(result).toBeInstanceOf(Blob);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/preview-with-images",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("seedDefaultTemplates", () => {
    it("sends POST to /seed-defaults and returns seeded templates", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeTemplate()]), { status: 200 })
      );

      const result = await seedDefaultTemplates();
      expect(result).toHaveLength(1);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/seed-defaults",
        expect.objectContaining({ method: "POST" })
      );
    });
  });

  describe("restoreDefaultTemplates", () => {
    it("sends POST to /restore-defaults and returns restored templates", async () => {
      mockApiFetch.mockResolvedValueOnce(
        new Response(JSON.stringify([makeTemplate()]), { status: 200 })
      );

      const result = await restoreDefaultTemplates();
      expect(result).toHaveLength(1);
      expect(mockApiFetch).toHaveBeenCalledWith(
        "/api/templates/restore-defaults",
        expect.objectContaining({ method: "POST" })
      );
    });
  });
});
