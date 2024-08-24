import { beforeAll, describe, test, vi } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { m4dContext } from "@/helpers/TestHelpers";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("Song Merge", () => {
  beforeAll(() => {
    mockResizObserver();
    vi.useFakeTimers().setSystemTime(new Date("2022-02-07").getTime());
  });

  test("Renders the Merge Page", () => {
    testPageSnapshot(App, model, m4dContext());
  });
});
