import { describe, expect, test } from "vitest";
import { mount } from "@vue/test-utils";
import DanceName from "@/components/DanceName.vue";
import { TempoType } from "@/models/DanceDatabase/TempoType";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { DanceType } from "@/models/DanceDatabase/DanceType";
import { TempoRange } from "@/models/DanceDatabase/TempoRange";
import { Meter } from "@/models/DanceDatabase/Meter";

const dance = new DanceType({
  name: "test-dance",
  tempoRange: new TempoRange(100, 110),
  meter: new Meter(4, 4),
});
const infiniteDance = new DanceType({ name: "test-dance", tempoRange: new TempoRange(0, 10000) });

describe("DanceName.vue", () => {
  test("computes danceLink correctly", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
      },
    });

    expect(wrapper.vm.danceLink).toBe("/dances/test-dance");
  });

  test("computes canShowTempo correctly for finite tempo", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
      },
    });

    expect(wrapper.vm.canShowTempo).toBe(true);
  });

  test("computes canShowTempo correctly for infinite tempo", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: infiniteDance,
      },
    });

    expect(wrapper.vm.canShowTempo).toBe(false);
  });

  test("computes canShowTempo correctly for dance group", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: new DanceGroup({ name: "test-group", danceIds: ["test"] }),
      },
    });

    expect(wrapper.vm.canShowTempo).toBe(false);
  });

  test("computes tempoText correctly", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
        showTempo: TempoType.Both,
      },
    });

    expect(wrapper.vm.tempoText).toBe("100-110 BPM/25-27.5 MPM");
  });

  test("renders a dance name correctly without tempo", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
        showTempo: TempoType.None,
      },
    });

    expect(wrapper.html()).toMatchSnapshot();
  });

  test("renders a dance name correctly with tempo", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
        showTempo: TempoType.Both,
      },
    });

    expect(wrapper.html()).toMatchSnapshot();
  });

  test("renders a dance name correctly with multiline", () => {
    const wrapper = mount(DanceName, {
      props: {
        dance: dance,
        showTempo: TempoType.Both,
        multiLine: true,
      },
    });

    expect(wrapper.html()).toMatchSnapshot();
  });
});
