import { describe, test, expect, beforeEach, vi } from "vitest";
import { nextTick } from "vue";
import type { VueWrapper } from "@vue/test-utils";
import App from "../App.vue";
import { loadTestPage } from "@/helpers/TestPageSnapshot";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { MenuContext } from "@/models/MenuContext";
import {
  DEFAULT_COUNT_METHOD,
  DEFAULT_EPSILON,
  DEFAULT_NUMERATOR,
  DEFAULT_TEMPO,
} from "../QueryValidation";
import { defaultDeltaMetrics } from "../deltaMetrics";

const mockContext = new MenuContext({
  userName: "dwgray",
  roles: [],
  xsrfToken: "TEST_XSRF",
});

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
}));

function mountTempoCounter(
  model: Record<string, unknown> = {},
): VueWrapper<InstanceType<typeof App>> {
  return loadTestPage(App, model) as unknown as VueWrapper<InstanceType<typeof App>>;
}

describe("tempo-counter App.vue", () => {
  beforeEach(() => {
    mockResizObserver();
  });

  describe("query-parameter validation/fencing", () => {
    test("defaults every value when no model is provided", () => {
      const wrapper = mountTempoCounter();

      expect(wrapper.vm.beatsPerMeasure).toBe(DEFAULT_NUMERATOR);
      expect(wrapper.vm.beatsPerMinute).toBe(DEFAULT_TEMPO);
      expect(wrapper.vm.countMethod).toBe(DEFAULT_COUNT_METHOD);
      expect(wrapper.vm.epsilonPercent).toBe(DEFAULT_EPSILON);
    });

    test("seeds every value from a valid server-provided model", () => {
      const wrapper = mountTempoCounter({
        numerator: 3,
        tempo: 124,
        count: "measures",
        epsilon: 12,
      });

      expect(wrapper.vm.beatsPerMeasure).toBe(3);
      expect(wrapper.vm.beatsPerMinute).toBe(124);
      expect(wrapper.vm.countMethod).toBe("measures");
      expect(wrapper.vm.epsilonPercent).toBe(12);
    });

    test("an invalid numerator (not one of the 2/3/4 meters this page offers) falls back to the default", () => {
      const wrapper = mountTempoCounter({ numerator: 7 });

      expect(wrapper.vm.beatsPerMeasure).toBe(DEFAULT_NUMERATOR);
    });

    test("a negative or out-of-range tempo falls back to the default", () => {
      expect(mountTempoCounter({ tempo: -5 }).vm.beatsPerMinute).toBe(DEFAULT_TEMPO);
      expect(mountTempoCounter({ tempo: 501 }).vm.beatsPerMinute).toBe(DEFAULT_TEMPO);
    });

    test("a non-finite tempo falls back to the default", () => {
      expect(mountTempoCounter({ tempo: Number.NaN }).vm.beatsPerMinute).toBe(DEFAULT_TEMPO);
    });

    test("an unrecognized count method falls back to 'beats' rather than being trusted as-is", () => {
      const wrapper = mountTempoCounter({ count: "not-a-real-count-method" });

      expect(wrapper.vm.countMethod).toBe("beats");
    });

    test("an out-of-range epsilon falls back to the default", () => {
      expect(mountTempoCounter({ epsilon: -1 }).vm.epsilonPercent).toBe(DEFAULT_EPSILON);
      expect(mountTempoCounter({ epsilon: 25 }).vm.epsilonPercent).toBe(DEFAULT_EPSILON);
    });
  });

  describe("measures/beats bridging", () => {
    test("measuresPerMinute is derived from beatsPerMinute and beatsPerMeasure", () => {
      const wrapper = mountTempoCounter({ numerator: 3, tempo: 90 });

      expect(wrapper.vm.measuresPerMinute).toBe(30);
    });

    test("setting measuresPerMinute writes back through to beatsPerMinute", () => {
      const wrapper = mountTempoCounter({ numerator: 3 });

      wrapper.vm.measuresPerMinute = 40;

      expect(wrapper.vm.beatsPerMinute).toBe(120);
    });

    test("tempoType follows countMethod", () => {
      const wrapper = mountTempoCounter();

      expect(wrapper.vm.tempoType).toBe(TempoType.Beats);

      wrapper.vm.countMethod = "measures";

      expect(wrapper.vm.tempoType).toBe(TempoType.Measures);
    });
  });

  describe("dance matching (DanceDeltas)", () => {
    test("the default zero tempo matches nothing", () => {
      const wrapper = mountTempoCounter();

      expect(wrapper.text()).not.toContain("Cha Cha");
    });

    test("a tempo within a dance's range matches it, regardless of epsilon", () => {
      // Cha Cha's dance-level tempoRange (union across its American Rhythm/International
      // Latin/Country instances) is 100-128 BPM; 124 falls inside it, so delta is 0.
      const wrapper = mountTempoCounter({ numerator: 4, tempo: 124, epsilon: 5 });

      expect(wrapper.text()).toContain("Cha Cha");
    });

    test("the meter pre-filter excludes a dance whose meter doesn't match the selected numerator", () => {
      // Cha Cha is 4/4; selecting numerator 3 (the 3/4-only filter) must exclude it even though
      // its own tempo would otherwise match.
      const wrapper = mountTempoCounter({ numerator: 3, tempo: 124, epsilon: 5 });

      expect(wrapper.text()).not.toContain("Cha Cha");
    });

    test("epsilon is a live inclusion threshold, not just a display cutoff", () => {
      // 140 BPM is 12 above Cha Cha's max of 128, i.e. (12 * 100) / 128 = 9.375% over.
      const justOutside = mountTempoCounter({ numerator: 4, tempo: 140, epsilon: 9 });
      const justInside = mountTempoCounter({ numerator: 4, tempo: 140, epsilon: 10 });

      expect(justOutside.text()).not.toContain("Cha Cha");
      expect(justInside.text()).toContain("Cha Cha");
    });

    test("an out-of-range match shows the pitch-control percentage that brings it into range", () => {
      // 140 BPM against Cha Cha's max of 128: 128 / 140 = 91.43% speed, i.e. -8.6% pitch.
      const wrapper = mountTempoCounter({ numerator: 4, tempo: 140, epsilon: 10, show: ["pitch"] });

      expect(wrapper.text()).toContain("Pitch −8.6%");
    });
  });

  describe("row highlighting and badges at 93 BPM", () => {
    function rows() {
      const wrapper = mountTempoCounter({
        numerator: 4,
        tempo: 93,
        epsilon: 5,
        show: ["tempo", "pitch"],
      });
      return wrapper.findAll(".list-group-item").map((r) => ({
        text: r.text(),
        variant: r.classes().find((c) => /^list-group-item-(primary|success|warning)$/.test(c)),
        badges: r.findAll(".badge").map((b) => b.text()),
      }));
    }

    function row(name: string) {
      const found = rows().find((r) => r.text.startsWith(name));
      if (!found) {
        throw new Error(`No ${name} row found`);
      }
      return found;
    }

    test("a dance whose range contains the tempo is unhighlighted and has no badges at all", () => {
      // Badges must be absent from the DOM, not just hidden: they sit in a d-flex wrapper, and
      // Bootstrap's display:flex !important beats v-show's inline display:none.
      expect(row("West Coast Swing")).toMatchObject({
        variant: "list-group-item-primary",
        badges: [],
      });
    });

    test("a near miss in 4/4 is highlighted even when it's under 1 MPM out", () => {
      // NCS tops out at 92 BPM (1 BPM = 0.25 MPM over); Bolero starts at 96 (0.75 MPM under).
      expect(row("Night Club Two Step")).toMatchObject({
        variant: "list-group-item-success",
        badges: ["1.0 BPM fast", "Pitch −1.1%"],
      });
      expect(row("Bolero")).toMatchObject({
        variant: "list-group-item-warning",
        badges: ["3.0 BPM slow", "Pitch +3.2%"],
      });
    });

    test("the same BPM miss gets the same highlight regardless of the dance's meter", () => {
      // Samba (2/4) and Bolero (4/4) share a 96-104 BPM range.
      expect(row("Samba").variant).toBe(row("Bolero").variant);
    });
  });

  describe("which details to show (?show=)", () => {
    // 140 BPM against Cha Cha's max of 128 is 3.0 MPM fast, -8.6% pitch, 91.4% speed.
    const nearMiss = { numerator: 4, tempo: 140, count: "measures", epsilon: 10 };

    // Scoped to one row's badges: the page text also contains the chooser's option labels.
    function chaChaBadges(wrapper: VueWrapper<InstanceType<typeof App>>): string[] {
      const row = wrapper.findAll(".list-group-item").find((r) => r.text().includes("Cha Cha"));
      if (!row) {
        throw new Error("No Cha Cha row found");
      }
      return row.findAll(".badge").map((b) => b.text());
    }

    test("defaults to just the tempo difference, with pitch and speed opt-in", () => {
      const wrapper = mountTempoCounter(nearMiss);

      expect(wrapper.vm.metrics).toEqual(defaultDeltaMetrics);
      expect(defaultDeltaMetrics).toEqual(["tempo"]);
      expect(chaChaBadges(wrapper)).toEqual(["3.0 MPM fast"]);
      expect(wrapper.text()).not.toContain("tempo-change software");
    });

    test("seeds the shown details from the server-provided model", () => {
      const wrapper = mountTempoCounter({ ...nearMiss, show: ["speed"] });

      expect(chaChaBadges(wrapper)).toEqual(["Speed 91.4%"]);
    });

    test("all three details render in a fixed order regardless of the seeded order", () => {
      const wrapper = mountTempoCounter({ ...nearMiss, show: ["speed", "pitch", "tempo"] });

      expect(chaChaBadges(wrapper)).toEqual(["3.0 MPM fast", "Pitch −8.6%", "Speed 91.4%"]);
    });

    test("unknown keys are silently dropped", () => {
      const wrapper = mountTempoCounter({ ...nearMiss, show: ["speed", "not-a-real-metric"] });

      expect(wrapper.vm.metrics).toEqual(["speed"]);
    });

    test("the explanatory note only appears when pitch or speed is shown", () => {
      expect(mountTempoCounter({ ...nearMiss, show: ["tempo"] }).text()).not.toContain(
        "tempo-change software",
      );
      expect(mountTempoCounter({ ...nearMiss, show: ["speed"] }).text()).toContain(
        "tempo-change software",
      );
    });

    test("the address bar omits show at its default and lists a custom selection", async () => {
      const wrapper = mountTempoCounter(nearMiss);
      expect(new URLSearchParams(window.location.search).has("show")).toBe(false);

      wrapper.vm.metrics = ["tempo", "speed"];
      await nextTick();

      expect(new URLSearchParams(window.location.search).getAll("show")).toEqual([
        "tempo",
        "speed",
      ]);
    });
  });

  describe("shareable URL (useUrlQuerySync)", () => {
    test("the address bar reflects the seeded configuration as soon as the page mounts", () => {
      mountTempoCounter({ numerator: 3, tempo: 90, count: "measures", epsilon: 8 });

      const params = new URLSearchParams(window.location.search);
      expect(params.get("numerator")).toBe("3");
      expect(params.get("tempo")).toBe("90.0");
      expect(params.get("count")).toBe("measures");
      expect(params.get("epsilon")).toBe("8");
    });

    test("the address bar updates live as the user adjusts the strictness slider", async () => {
      const wrapper = mountTempoCounter();

      wrapper.vm.epsilonPercent = 12;
      await nextTick();

      expect(new URLSearchParams(window.location.search).get("epsilon")).toBe("12");
    });

    test("the tempo param is rounded to the one decimal place the UX displays", async () => {
      const wrapper = mountTempoCounter();

      wrapper.vm.beatsPerMinute = 123.456;
      await nextTick();

      expect(new URLSearchParams(window.location.search).get("tempo")).toBe("123.5");
    });
  });

  describe("choosing a dance", () => {
    test("opens the dance's page in a new tab", () => {
      const openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
      const wrapper = mountTempoCounter();

      wrapper.vm.chooseDance("CHA");

      expect(openSpy).toHaveBeenCalledWith("/dances/cha-cha", "_blank");
      openSpy.mockRestore();
    });

    test("does nothing for an unrecognized dance id", () => {
      const openSpy = vi.spyOn(window, "open").mockImplementation(() => null);
      const wrapper = mountTempoCounter();

      wrapper.vm.chooseDance("NOT-A-REAL-DANCE-ID");

      expect(openSpy).not.toHaveBeenCalled();
      openSpy.mockRestore();
    });
  });
});
