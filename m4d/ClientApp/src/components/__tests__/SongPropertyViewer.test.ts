import { describe, it, expect } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { SongProperty } from "@/models/SongProperty";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import SongPropertyViewer from "../SongPropertyViewer.vue";
import TagViewer from "../TagViewer.vue";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function mountViewer(property: SongProperty, activeTags?: Set<string>) {
  return shallowMount(SongPropertyViewer, {
    props: activeTags ? { property, activeTags } : { property },
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("SongPropertyViewer.vue", () => {
  describe("initial rendering", () => {
    it("renders tempo text for a Tempo property", () => {
      const wrapper = mountViewer(new SongProperty({ name: "Tempo", value: "174.0" }));
      expect(wrapper.text()).toContain("tempo = 174.0 BPM");
    });

    it("renders dance viewer stub for a Tag+ dance property", () => {
      const wrapper = mountViewer(new SongProperty({ name: "Tag+", value: "Jive:Dance" }));
      expect(wrapper.find("dance-viewer-stub").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("BPM");
    });

    it("renders tag viewer stub for a Tag+ music property", () => {
      const wrapper = mountViewer(new SongProperty({ name: "Tag+", value: "Pop:Music" }));
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(true);
    });

    it("renders removal (Tag-) with added=false", () => {
      const wrapper = mountViewer(new SongProperty({ name: "Tag-", value: "Pop:Music" }));
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(true);
      expect(wrapper.findComponent(TagViewer).props("added")).toBe(false);
    });

    it("passes danceId for dance-qualified tags (Tag+:JIV)", () => {
      const wrapper = mountViewer(new SongProperty({ name: "Tag+:JIV", value: "Modern:Style" }));
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(true);
      expect(wrapper.findComponent(TagViewer).props("danceId")).toBe("JIV");
    });
  });

  describe("prop reactivity — BUG REPRO", () => {
    // These tests demonstrate the reactivity bug in SongPropertyViewer:
    // derived values (tags, isAdd, danceId, isTempo, isComment) were computed
    // once at setup time as plain variables, not as computed(). When the
    // `property` prop changes (because Vue reuses the component instance via
    // :key="index" in the parent v-for), the stale values remain and the
    // template renders incorrect content.

    it("AdamT: switches from Tempo display to Tag+ display when property prop changes", async () => {
      // Simulate the case where a SongPropertyViewer instance that rendered
      // AdamT's Tempo property gets reused for a Tag+ property (e.g., when the
      // parent v-for changes the displayed change set).
      const tempoProperty = new SongProperty({ name: "Tempo", value: "174.0" });
      const tagProperty = new SongProperty({ name: "Tag+", value: "Jive:Dance" });

      const wrapper = mountViewer(tempoProperty);
      expect(wrapper.text()).toContain("tempo = 174.0 BPM");

      await wrapper.setProps({ property: tagProperty });

      // After prop change, should render the dance tag — NOT the old tempo text
      expect(wrapper.text()).not.toContain("BPM");
      expect(wrapper.text()).not.toContain("174.0");
      expect(wrapper.find("dance-viewer-stub").exists()).toBe(true);
    });

    it("AdamT: Tag+:JIV (style tag) switches correctly to Tempo when property changes", async () => {
      // The AdamT entry has Tag+:JIV=Modern:Style — a dance-qualified tag.
      // If that SongPropertyViewer gets reused for a Tempo property, isTempo
      // must update correctly.
      const styleTagProperty = new SongProperty({ name: "Tag+:JIV", value: "Modern:Style" });
      const tempoProperty = new SongProperty({ name: "Tempo", value: "173.1" });

      const wrapper = mountViewer(styleTagProperty);
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("BPM");

      await wrapper.setProps({ property: tempoProperty });

      // Must switch to tempo display; NOT the old tag display
      expect(wrapper.text()).toContain("tempo = 173.1 BPM");
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(false);
    });

    it("algorithmic: updates tag content when property value changes (Pop:Music → Dance And Dj:Music)", async () => {
      // Simulates batch|P → batch-a|P index reuse: same property type (Tag+)
      // but different tag content. `tags` must recompute.
      const popMusic = new SongProperty({ name: "Tag+", value: "Pop:Music" });
      const danceMusic = new SongProperty({ name: "Tag+", value: "Dance And Dj:Music" });

      const wrapper = mountViewer(popMusic);
      const firstStub = wrapper.find("tag-viewer-stub");
      expect(firstStub.attributes("tag")).toContain("Pop");

      await wrapper.setProps({ property: danceMusic });

      // Tag content must update — old "Pop:Music" tag gone, new "Dance And Dj:Music" shown
      const updatedStub = wrapper.find("tag-viewer-stub");
      expect(updatedStub.attributes("tag")).toContain("Dance And Dj");
      expect(updatedStub.attributes("tag")).not.toContain("Pop");
    });

    it("algorithmic: switches from dance tag to music tag when property changes", async () => {
      // batch-e|P has Tempo; other algo users have Tag+(Music). Verify the
      // viewer type switches correctly (DanceViewer → TagViewer).
      const danceTag = new SongProperty({ name: "Tag+", value: "East Coast Swing:Dance" });
      const musicTag = new SongProperty({ name: "Tag+", value: "Pop:Music" });

      const wrapper = mountViewer(danceTag);
      expect(wrapper.find("dance-viewer-stub").exists()).toBe(true);
      expect(wrapper.find("tag-viewer-stub").exists()).toBe(false);

      await wrapper.setProps({ property: musicTag });

      expect(wrapper.find("tag-viewer-stub").exists()).toBe(true);
      expect(wrapper.find("dance-viewer-stub").exists()).toBe(false);
    });

    it("algorithmic: isAdd (added/removed icon) updates when property changes from Tag+ to Tag-", async () => {
      const addProp = new SongProperty({ name: "Tag+", value: "Pop:Music" });
      const removeProp = new SongProperty({ name: "Tag-", value: "Pop:Music" });

      const wrapper = mountViewer(addProp);
      expect(wrapper.findComponent(TagViewer).props("added")).toBe(true);

      await wrapper.setProps({ property: removeProp });

      expect(wrapper.findComponent(TagViewer).props("added")).toBe(false);
    });

    it("danceId clears when switching from dance-qualified Tag+:JIV to plain Tag+", async () => {
      // AdamT's Tag+:JIV=Modern:Style followed by reuse for a plain tag
      const qualifiedProp = new SongProperty({ name: "Tag+:JIV", value: "Modern:Style" });
      const plainProp = new SongProperty({ name: "Tag+", value: "Pop:Music" });

      const wrapper = mountViewer(qualifiedProp);
      expect(wrapper.findComponent(TagViewer).props("danceId")).toBe("JIV");

      await wrapper.setProps({ property: plainProp });

      expect(wrapper.findComponent(TagViewer).props("danceId")).toBeUndefined();
    });
  });
});
