import { afterAll, beforeAll, describe, test, vi } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Playlist Viewer", () => {
  beforeAll(() => {
    vi.useFakeTimers();
    // Keep this far enough from June 2025 entries to remain in "months" across environments.
    vi.setSystemTime(new Date("2026-05-01T12:00:00"));
  });

  afterAll(() => {
    vi.useRealTimers();
  });

  test(
    "renders a playlist viewer index page",
    () => {
      testPageSnapshot(App, model);
    },
    50000,
  );
});
