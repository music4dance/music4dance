import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { Tag } from "@/models/Tag";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import TagViewer from "../TagViewer.vue";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function mountViewer(props: {
  tag: Tag;
  added?: boolean;
  danceId?: string;
  activeTags?: Set<string>;
}) {
  return mount(TagViewer, { props });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("TagViewer.vue", () => {
  describe("isRemoved / strike-through behavior", () => {
    // BUG REPRODUCTION: Dance-specific style tags (e.g. Tag+:JIV=Modern:Style)
    // should NEVER show as struck-through. activeTags only tracks song-level tags,
    // so a danceId-qualified tag like "Modern:Style" is never in the set.
    // Without the danceId guard, isRemoved is always true for these tags.
    it("does NOT strike through a dance-qualified tag (danceId set), even when not in activeTags", () => {
      // "Modern:Style" from Tag+:JIV=Modern:Style — activeTags won't contain it
      const wrapper = mountViewer({
        tag: Tag.fromParts("Modern", "Style"),
        added: true,
        danceId: "JIV",
        activeTags: new Set(["Jive:Dance", "Pop:Music"]), // "Modern:Style" NOT present
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });

    it("does NOT strike through International:Style (AdamT / BrittanyFalconer pattern)", () => {
      const wrapper = mountViewer({
        tag: Tag.fromParts("International", "Style"),
        added: true,
        danceId: "JIV",
        activeTags: new Set(["Jive:Dance", "4/4:Tempo", "Pop:Music"]),
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });

    it("does NOT strike through a dance-other tag like Alan:Other on a dance", () => {
      // Mirrors the DWTS entry: Tag+:JAZ=Alan:Other|Alexis:Other
      const wrapper = mountViewer({
        tag: Tag.fromParts("Alan", "Other"),
        added: true,
        danceId: "JAZ",
        activeTags: new Set(["Jazz:Dance"]),
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });

    it("DOES strike through a song-level tag that is no longer active", () => {
      // A regular Tag+ (no danceId) for a tag no longer in activeTags → should be struck through
      const wrapper = mountViewer({
        tag: Tag.fromParts("Pop", "Music"),
        added: true,
        activeTags: new Set(["Jive:Dance"]), // "Pop:Music" not present
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(true);
    });

    it("does NOT strike through an active song-level tag (tag IS in activeTags)", () => {
      const wrapper = mountViewer({
        tag: Tag.fromParts("Pop", "Music"),
        added: true,
        activeTags: new Set(["Pop:Music", "Jive:Dance"]),
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });

    it("does NOT strike through when activeTags is not provided", () => {
      const wrapper = mountViewer({
        tag: Tag.fromParts("Modern", "Style"),
        added: true,
        danceId: "JIV",
        // activeTags omitted
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });

    it("does NOT strike through a Tag- (removal) entry, even when not in activeTags", () => {
      // added=false means this is a removal row — should never be struck through
      const wrapper = mountViewer({
        tag: Tag.fromParts("Pop", "Music"),
        added: false,
        activeTags: new Set([]),
      });
      expect(wrapper.find(".text-decoration-line-through").exists()).toBe(false);
    });
  });
});
