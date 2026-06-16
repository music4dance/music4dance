import { beforeAll, beforeEach, afterEach, describe, test, expect } from "vitest";
import { testPageSnapshot, loadTestPage } from "@/helpers/TestPageSnapshot";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { SongFilter } from "@/models/SongFilter";
import App from "../App.vue";

describe("AdvancedSearch.vue", () => {
  const originalWindowLocation = window.location;

  beforeAll(() => {
    mockResizObserver();
  });

  beforeEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        "https://localhost:5001/song/advancedsearchform?filter=v2-Advanced-BOL%2CRMB--Love",
      ),
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: originalWindowLocation,
    });
  });

  test("renders the advanced search page", async () => {
    testPageSnapshot(App);
  });

  test("renders dance group filters without crashing", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "WLZ";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    expect(wrapper.text()).toContain("Tempo range (BPM):");
  });
});
