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

  test("hides the scope-dance selector when only one dance is selected", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    expect(wrapper.find("#scope-dance-group").exists()).toBe(false);
    expect(wrapper.text()).toContain("Tempo range for Bolero (BPM):");
  });

  test("shows the scope-dance selector once two or more dances are selected", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    expect(wrapper.find("#scope-dance-group").exists()).toBe(true);
    expect(wrapper.text()).toContain("Tempo range (BPM):");

    const options = wrapper.find("#scope-dance").findAll("option");
    expect(options.map((o) => o.text())).toEqual(["Overall (default)", "Bolero", "Rumba"]);
  });

  test("initializes the scope-dance selector from an explicitly marked dance in the filter", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB*";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    expect((wrapper.find("#scope-dance").element as HTMLSelectElement).value).toEqual("RMB");
    expect(wrapper.text()).toContain("Tempo range for Rumba (BPM):");
  });

  test("selecting a scope dance marks it as primary in the assembled filter", async () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}&showDiagnostics=1`,
      ),
    });

    const wrapper = loadTestPage(App);
    await wrapper.find("#scope-dance").setValue("RMB");

    expect(wrapper.text()).toContain("Tempo range for Rumba (BPM):");
    expect(wrapper.find("pre").text()).toContain("BOL,RMB*");
  });

  function sortOptionTexts(wrapper: ReturnType<typeof loadTestPage>): string[] {
    return wrapper
      .find("#sort")
      .findAll("option")
      .map((o) => o.text());
  }

  test("names the scope dance in the Dance Rating sort option for a single-dance filter", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    const texts = sortOptionTexts(wrapper);

    expect(texts).toContain("Dance Rating (Bolero)");
    expect(texts).toContain("Default: Dance Rating (Bolero)");
  });

  test("leaves the Dance Rating sort option unqualified for multiple dances with no scope marker", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    const texts = sortOptionTexts(wrapper);

    expect(texts).toContain("Dance Rating");
    expect(texts).toContain("Default: Dance Rating");
  });

  test("names the scope dance in the Dance Rating sort option once one is marked", () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB*";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    const texts = sortOptionTexts(wrapper);

    expect(texts).toContain("Dance Rating (Rumba)");
  });

  test("updates the Dance Rating sort option as the scope-dance selection changes", async () => {
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.dances = "BOL,RMB";

    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        `https://localhost:5001/song/advancedsearchform?filter=${filter.encodedQuery}`,
      ),
    });

    const wrapper = loadTestPage(App);
    expect(sortOptionTexts(wrapper)).toContain("Dance Rating");

    await wrapper.find("#scope-dance").setValue("RMB");
    expect(sortOptionTexts(wrapper)).toContain("Dance Rating (Rumba)");
  });
});
