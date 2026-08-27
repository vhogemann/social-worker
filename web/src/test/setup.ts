import "@testing-library/jest-dom";
import React from "react";
import { vi } from "vitest";

if (typeof window !== "undefined") {
  (globalThis as any).ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
}

vi.mock("konva", () => {
  class MockStage {
    add = vi.fn();
    on = vi.fn();
    width = vi.fn();
    height = vi.fn();
    scale = vi.fn();
    batchDraw = vi.fn();
    destroy = vi.fn();
  }

  class MockLayer {
    add = vi.fn();
    getChildren = vi.fn().mockReturnValue([]);
    findOne = vi.fn();
    batchDraw = vi.fn();
  }

  class MockTransformer {
    nodes = vi.fn();
    moveToTop = vi.fn();
  }

  class MockShape {
    on = vi.fn();
    x = vi.fn().mockReturnValue(0);
    y = vi.fn().mockReturnValue(0);
    width = vi.fn().mockReturnValue(100);
    height = vi.fn().mockReturnValue(100);
    scaleX = vi.fn().mockReturnValue(1);
    scaleY = vi.fn().mockReturnValue(1);
    destroy = vi.fn();
  }

  return {
    default: {
      Stage: MockStage,
      Layer: MockLayer,
      Transformer: MockTransformer,
      Rect: MockShape,
      Circle: MockShape,
      Text: MockShape,
      Image: MockShape,
    },
  };
});
