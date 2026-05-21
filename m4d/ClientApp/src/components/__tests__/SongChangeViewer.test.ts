import { describe, it, expect, beforeAll } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { SongChange } from "@/models/SongChange";
import { SongProperty } from "@/models/SongProperty";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import SongChangeViewer from "../SongChangeViewer.vue";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeChange(overrides: {
  action?: string;
  user?: string;
  date?: Date;
  properties?: { name: string; value: string }[];
  actValue?: string;
}): SongChange {
  const props = (overrides.properties ?? []).map(
    (p) => new SongProperty({ name: p.name, value: p.value }),
  );
  return new SongChange(
    overrides.action ?? "Edit",
    props,
    overrides.user ?? "EthanH|P",
    overrides.date ?? new Date("2014-03-17T17:46:07Z"),
    overrides.actValue ?? "",
  );
}

function mountViewer(change: SongChange, oneUser?: boolean) {
  return shallowMount(SongChangeViewer, {
    props: oneUser !== undefined ? { change, oneUser } : { change },
  });
}

// With shallowMount, child components render as <component-name-stub> tags.
// Icons: IBiCpuFill → ibi-cpu-fill-stub, IBiHeartFill → ibi-heart-fill-stub, etc.
// SongPropertyViewer → song-property-viewer-stub
// UserLink → user-link-stub

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("SongChangeViewer.vue", () => {
  describe("action text", () => {
    it("shows 'Added' for .Create actions", () => {
      const change = makeChange({ action: "Create" });
      const wrapper = mountViewer(change);
      expect(wrapper.text()).toContain("Added");
      expect(wrapper.text()).not.toContain("Changed");
    });

    it("shows 'Changed' for .Edit actions", () => {
      const change = makeChange({ action: "Edit" });
      const wrapper = mountViewer(change);
      expect(wrapper.text()).toContain("Changed");
    });
  });

  describe("user display", () => {
    it("shows 'by' and user link for normal change", () => {
      const change = makeChange({ user: "EthanH|P" });
      const wrapper = mountViewer(change);
      expect(wrapper.text()).toContain("by");
      expect(wrapper.find("user-link-stub").exists()).toBe(true);
    });

    it("hides 'by' and user link when oneUser=true", () => {
      const change = makeChange({ user: "EthanH|P" });
      const wrapper = mountViewer(change, true);
      expect(wrapper.text()).not.toContain("by");
      expect(wrapper.find("user-link-stub").exists()).toBe(false);
    });
  });

  describe("algorithmic indicator (cpu chip)", () => {
    it("shows chip for algorithmic change when oneUser is false", () => {
      const change = makeChange({ user: "batch-e|P" });
      const wrapper = mountViewer(change, false);
      expect(wrapper.find("bi-cpu-fill-stub").exists()).toBe(true);
    });

    it("does NOT show chip for human (non-algorithmic) change", () => {
      const change = makeChange({ user: "EthanH|P" });
      const wrapper = mountViewer(change);
      expect(wrapper.find("bi-cpu-fill-stub").exists()).toBe(false);
    });

    it("does NOT show chip for algorithmic change when oneUser=true", () => {
      const change = makeChange({ user: "batch-e|P" });
      const wrapper = mountViewer(change, true);
      expect(wrapper.find("bi-cpu-fill-stub").exists()).toBe(false);
    });

    it("does NOT show chip for Catalog (batch|P) \u2014 not algorithmic", () => {
      const change = makeChange({ user: "batch|P" });
      const wrapper = mountViewer(change);
      expect(wrapper.find("bi-cpu-fill-stub").exists()).toBe(false);
    });
  });

  describe("like/dislike icons", () => {
    it("shows heart-fill icon for a Like=True property", () => {
      const change = makeChange({
        user: "dwgray",
        properties: [{ name: "Like", value: "True" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.find("bi-heart-fill-stub").exists()).toBe(true);
      expect(wrapper.find("bi-heartbreak-fill-stub").exists()).toBe(false);
    });

    it("shows heartbreak icon for a Like=False property", () => {
      const change = makeChange({
        user: "dwgray",
        properties: [{ name: "Like", value: "False" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.find("bi-heartbreak-fill-stub").exists()).toBe(true);
      expect(wrapper.find("bi-heart-fill-stub").exists()).toBe(false);
    });

    it("shows pencil icon when there is no like property", () => {
      const change = makeChange({
        user: "EthanH|P",
        properties: [{ name: "Tag+", value: "East Coast Swing:Dance" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.find("bi-pencil-stub").exists()).toBe(true);
      expect(wrapper.find("bi-heart-fill-stub").exists()).toBe(false);
    });
  });

  describe("viewable properties", () => {
    it("renders Tag+ properties as SongPropertyViewer elements", () => {
      const change = makeChange({
        properties: [
          { name: "Tag+", value: "East Coast Swing:Dance" },
          { name: "Tag-", value: "Lindy Hop:Dance" },
        ],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(2);
    });

    it("renders Tempo property as SongPropertyViewer", () => {
      const change = makeChange({
        properties: [{ name: "Tempo", value: "174.0" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(1);
    });

    it("renders Comment+ property as SongPropertyViewer", () => {
      const change = makeChange({
        properties: [{ name: "Comment+", value: "Great song!" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(1);
    });

    it("does NOT render Album property (not viewable)", () => {
      const change = makeChange({
        properties: [{ name: "Album:0", value: "Back To Basics" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(0);
    });

    it("does NOT render DanceRating property (not viewable)", () => {
      const change = makeChange({
        properties: [{ name: "DanceRating", value: "ECS+1" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(0);
    });

    it("renders dance-specific tag (Tag+:JIV) as SongPropertyViewer", () => {
      const change = makeChange({
        properties: [{ name: "Tag+:JIV", value: "International:Style" }],
      });
      const wrapper = mountViewer(change);
      expect(wrapper.findAll("song-property-viewer-stub")).toHaveLength(1);
    });
  });

  describe("date display", () => {
    it("shows the formatted date", () => {
      const change = makeChange({ date: new Date("2014-03-17T17:46:07Z") });
      const wrapper = mountViewer(change);
      expect(wrapper.text()).toContain("on");
    });

    it("shows '<unknown>' when no date is provided", () => {
      const change = new SongChange("Edit", [], "EthanH|P", undefined, "");
      const wrapper = mountViewer(change);
      expect(wrapper.text()).toContain("<unknown>");
    });
  });
});
