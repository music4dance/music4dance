import { afterEach, beforeAll, describe, test, vi } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { m4dContext } from "@/helpers/TestHelpers";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("Song Merge", () => {
  beforeAll(() => {
    mockResizObserver();
    //vi.useFakeTimers().setSystemTime(new Date(Date.UTC(2022, 1, 7, 0, 0, 0)));
  });
  afterEach(() => {
    vi.restoreAllMocks();
  });

  test(
    "Renders the Merge Page",
    () => {
      vi.mock("@/helpers/timeHelpers.ts", async (importOriginal) => {
        const actual = await importOriginal();
        if (typeof actual === "object" && actual !== null) {
          return {
            ...actual,
            formatNow: vi.fn().mockReturnValue("07-Feb-2014"),
          };
        }
        return actual;
      });
      testPageSnapshot(App, model, m4dContext());
    },
    50000,
  );
});
