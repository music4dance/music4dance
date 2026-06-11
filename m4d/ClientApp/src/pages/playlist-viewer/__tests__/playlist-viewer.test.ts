import { afterAll, beforeAll, describe, test, vi } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Playlist Viewer", () => {
  beforeAll(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-06-10T12:00:00Z"));
  });

  afterAll(() => {
    vi.useRealTimers();
  });

  test("renders a playlist viewer index page", () => {
    testPageSnapshot(App, model);
  }, 50000);
});
