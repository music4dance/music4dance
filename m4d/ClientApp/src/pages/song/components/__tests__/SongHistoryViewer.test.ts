import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import SongHistoryViewer from "../SongHistoryViewer.vue";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const stubComponents = {
  BCard: {
    template: '<div class="b-card"><slot name="header" /><slot /></div>',
  },
  BFormCheckbox: {
    template:
      '<label class="b-form-checkbox"><input type="checkbox" :checked="modelValue" @change="$emit(\'update:modelValue\', $event.target.checked)" /><slot /></label>',
    props: ["modelValue"],
    emits: ["update:modelValue"],
  },
  BListGroup: { template: '<ul class="b-list-group"><slot /></ul>' },
  BListGroupItem: { template: '<li class="b-list-group-item"><slot /></li>' },
  SongChangeViewer: {
    template: '<div class="song-change-viewer" :data-user="change.user" />',
    props: ["change"],
  },
  IBiCpuFill: { template: '<span class="bi-cpu-fill" />' },
};

function makeHistory(props: { name: string; value: string }[]): SongHistory {
  return new SongHistory({
    id: "test-id",
    properties: props.map((p) => new SongProperty({ name: p.name, value: p.value })),
  });
}

const humanProps = [
  { name: ".Create", value: "" },
  { name: "User", value: "EthanH|P" },
  { name: "Time", value: "3/17/2014 5:46:07 PM" },
  { name: "Tag+", value: "East Coast Swing:Dance" },
  { name: ".Edit", value: "" },
  { name: "User", value: "JuliaS|P" },
  { name: "Time", value: "6/5/2014 8:46:10 PM" },
  { name: "Tag+", value: "Lindy Hop:Dance" },
];

const batchProps = [
  { name: ".Edit", value: "" },
  { name: "User", value: "batch|P" },
  { name: "Time", value: "11/20/2014 11:30:37 AM" },
  { name: "Tag+", value: "Pop:Music" },
];

const algoTempoProps = [
  { name: ".Edit", value: "" },
  { name: "User", value: "batch-e|P" },
  { name: "Time", value: "02/09/2016 21:16:12" },
  { name: "Tempo", value: "173.1" },
];

function mountViewer(history: SongHistory, authenticated: boolean) {
  return mount(SongHistoryViewer, {
    props: { history, authenticated },
    global: { stubs: stubComponents },
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("SongHistoryViewer.vue", () => {
  describe("unauthenticated", () => {
    it("shows only human user changes (userChanges)", () => {
      const history = makeHistory([...humanProps, ...batchProps]);
      const wrapper = mountViewer(history, false);
      const items = wrapper.findAll(".song-change-viewer");
      expect(items).toHaveLength(2); // EthanH|P and JuliaS|P
      const users = items.map((i) => i.attributes("data-user"));
      expect(users).toContain("EthanH|P");
      expect(users).toContain("JuliaS|P");
      expect(users).not.toContain("batch|P");
    });

    it("does NOT show the automated toggle", () => {
      const history = makeHistory(humanProps);
      const wrapper = mountViewer(history, false);
      expect(wrapper.find(".b-form-checkbox").exists()).toBe(false);
    });
  });

  describe("authenticated, toggle off (default)", () => {
    it("shows only human user changes by default", () => {
      const history = makeHistory([...humanProps, ...batchProps, ...algoTempoProps]);
      const wrapper = mountViewer(history, true);
      const items = wrapper.findAll(".song-change-viewer");
      const users = items.map((i) => i.attributes("data-user"));
      expect(users).toContain("EthanH|P");
      expect(users).toContain("JuliaS|P");
      expect(users).not.toContain("batch|P");
      expect(users).not.toContain("batch-e|P");
    });

    it("shows the automated toggle", () => {
      const history = makeHistory(humanProps);
      const wrapper = mountViewer(history, true);
      expect(wrapper.find(".b-form-checkbox").exists()).toBe(true);
    });

    it("toggle label mentions 'automated'", () => {
      const history = makeHistory(humanProps);
      const wrapper = mountViewer(history, true);
      expect(wrapper.find(".b-form-checkbox").text().toLowerCase()).toContain("automated");
    });
  });

  describe("authenticated, toggle on", () => {
    async function mountWithToggleOn(history: SongHistory) {
      const wrapper = mountViewer(history, true);
      const checkbox = wrapper.find(".b-form-checkbox input");
      await checkbox.setValue(true);
      await wrapper.vm.$nextTick();
      return wrapper;
    }

    it("shows batch|P (Catalog) changes when toggle is on", async () => {
      const history = makeHistory([...humanProps, ...batchProps]);
      const wrapper = await mountWithToggleOn(history);
      const users = wrapper.findAll(".song-change-viewer").map((i) => i.attributes("data-user"));
      expect(users).toContain("batch|P");
    });

    it("shows algorithmic changes when toggle is on", async () => {
      const history = makeHistory([...humanProps, ...algoTempoProps]);
      const wrapper = await mountWithToggleOn(history);
      const users = wrapper.findAll(".song-change-viewer").map((i) => i.attributes("data-user"));
      expect(users).toContain("batch-e|P");
    });

    it("still includes human changes when toggle is on", async () => {
      const history = makeHistory([...humanProps, ...batchProps]);
      const wrapper = await mountWithToggleOn(history);
      const users = wrapper.findAll(".song-change-viewer").map((i) => i.attributes("data-user"));
      expect(users).toContain("EthanH|P");
      expect(users).toContain("JuliaS|P");
    });

    it("shows more changes with toggle on than off", async () => {
      const history = makeHistory([...humanProps, ...batchProps, ...algoTempoProps]);
      const wrapperOff = mountViewer(history, true);
      const countOff = wrapperOff.findAll(".song-change-viewer").length;

      const wrapperOn = await mountWithToggleOn(history);
      const countOn = wrapperOn.findAll(".song-change-viewer").length;

      expect(countOn).toBeGreaterThan(countOff);
    });
  });

  describe("empty history", () => {
    it("renders with no change items when history has no viewable changes", () => {
      const emptyHistory = new SongHistory({ id: "empty", properties: [] });
      const wrapper = mountViewer(emptyHistory, false);
      expect(wrapper.findAll(".song-change-viewer")).toHaveLength(0);
    });

    it("shows 'Changes' header text", () => {
      const history = makeHistory(humanProps);
      const wrapper = mountViewer(history, true);
      expect(wrapper.text()).toContain("Changes");
    });
  });
});
