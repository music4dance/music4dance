import { describe, test, expect, beforeEach, vi } from "vitest";
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
