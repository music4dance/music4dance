import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty, PropertyType } from "@/models/SongProperty";
import { setupTestEnvironment, mockResizObserver } from "@/helpers/TestHelpers";
import WaltzCorrectionCard from "../WaltzCorrectionCard.vue";

setupTestEnvironment();
mockResizObserver();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build a minimal SongHistory with given tags and dance ratings */
function makeHistory(
  opts: {
    tags?: string; // e.g. "4/4:Tempo|Slow Waltz:Dance"
    danceRatings?: string[]; // e.g. ["SWZ+1", "VWZ+1"]
    tempo?: string;
  } = {},
): SongHistory {
  const { tags = "", danceRatings = [], tempo = "120" } = opts;
  const props: { name: string; value: string }[] = [
    { name: ".Create", value: "" },
    { name: "User", value: "testuser|P" },
    { name: "Time", value: "01/01/2024 12:00:00" },
    { name: "Title", value: "Test Song" },
    { name: "Artist", value: "Test Artist" },
    { name: "Tempo", value: tempo },
  ];
  if (tags) {
    props.push({ name: "Tag+", value: tags });
  }
  for (const dr of danceRatings) {
    props.push({ name: "DanceRating", value: dr });
  }
  return new SongHistory({
    id: "test-song-id",
    properties: props.map((p) => new SongProperty({ name: p.name, value: p.value })),
  });
}

interface MountOptions {
  history?: SongHistory;
  editing?: boolean;
  user?: string | null;
  canEdit?: boolean;
}

/** Mount WaltzCorrectionCard with a fresh editor derived from the given history */
function mountCard(opts: MountOptions = {}) {
  const {
    history = makeHistory({ tags: "4/4:Tempo|Slow Waltz:Dance", danceRatings: ["SWZ+1"] }),
    editing = false,
    user = "testuser",
    canEdit = true,
  } = opts;
  const song = Song.fromHistory(history, user ?? undefined);
  const editor = new SongEditor(undefined, user ?? undefined, history);
  return {
    wrapper: mount(WaltzCorrectionCard, {
      props: {
        song,
        editor,
        editing,
        user: user ?? undefined,
        canEdit,
      },
    }),
    editor,
    song,
  };
}

/** Find a property by name in the editor's edit history (properties added after save point) */
function findEditProp(editor: SongEditor, name: string): SongProperty | undefined {
  return editor.editHistory.properties.find((p) => p.name === name);
}

/** Find all properties by name in the editor's edit history */
function findAllEditProps(editor: SongEditor, name: string): SongProperty[] {
  return editor.editHistory.properties.filter((p) => p.name === name);
}

/** Get a button by index; throws if absent so the test fails with a clear message */
function getButton(wrapper: ReturnType<typeof mountCard>["wrapper"], index: number) {
  const button = wrapper.findAll("button")[index];
  if (!button) throw new Error(`No button at index ${index}`);
  return button;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("WaltzCorrectionCard.vue", () => {
  describe("visibility", () => {
    it("shows the card when song has a waltz rating and 4/4 tag, user is authenticated and canEdit", () => {
      const { wrapper } = mountCard();
      expect(wrapper.find(".card").exists()).toBe(true);
    });

    it("hides the card when user is null (unauthenticated)", () => {
      const { wrapper } = mountCard({ user: null });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("hides the card when canEdit is false", () => {
      const { wrapper } = mountCard({ canEdit: false });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("hides the card when editing is true", () => {
      const { wrapper } = mountCard({ editing: true });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("hides the card when the song has no waltz dance ratings", () => {
      const history = makeHistory({
        tags: "4/4:Tempo|East Coast Swing:Dance",
        danceRatings: ["ECS+1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("hides the card when the waltz dance rating is negative (weight ≤ 0)", () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Slow Waltz:Dance",
        danceRatings: ["SWZ-1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("hides the card when the song has a waltz rating but no 4/4 tag", () => {
      const history = makeHistory({
        tags: "Slow Waltz:Dance",
        danceRatings: ["SWZ+1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(false);
    });

    it("shows the card for CSW (Country Waltz)", () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Country Waltz:Dance",
        danceRatings: ["CSW+1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(true);
    });

    it("shows the card for VWZ (Viennese Waltz)", () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Viennese Waltz:Dance",
        danceRatings: ["VWZ+1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(true);
    });

    it("shows the card for TGV (Tango Vals — regression test)", () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Tango Vals:Dance",
        danceRatings: ["TGV+1"],
      });
      const { wrapper } = mountCard({ history });
      expect(wrapper.find(".card").exists()).toBe(true);
    });
  });

  describe("Case 1 — Fake (performer dances waltz to 4/4)", () => {
    it("adds Fake:Tempo tag for the waltz dance on click and emits edit", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 0).trigger("click");

      const fakeProp = findEditProp(editor, "Tag+:SWZ");
      expect(fakeProp).toBeDefined();
      expect(fakeProp?.value).toBe("Fake:Tempo");
      expect(wrapper.emitted("edit")).toBeTruthy();
    });

    it("adds Fake:Tempo to each waltz dance when multiple waltzes are present", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Slow Waltz:Dance|Viennese Waltz:Dance",
        danceRatings: ["SWZ+1", "VWZ+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 0).trigger("click");

      const swzProp = findEditProp(editor, "Tag+:SWZ");
      const vwzProp = findEditProp(editor, "Tag+:VWZ");
      expect(swzProp?.value).toBe("Fake:Tempo");
      expect(vwzProp?.value).toBe("Fake:Tempo");
    });

    it("adds Fake:Tempo for TGV (regression)", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Tango Vals:Dance",
        danceRatings: ["TGV+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 0).trigger("click");

      const tgvProp = findEditProp(editor, "Tag+:TGV");
      expect(tgvProp?.value).toBe("Fake:Tempo");
    });

    it("does NOT remove the 4/4:Tempo tag", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 0).trigger("click");

      const removeProp = findEditProp(editor, PropertyType.removedTags);
      expect(removeProp).toBeUndefined();
    });
  });

  describe("Case 2 — Bad meter (meter tag wrong, tempo value correct)", () => {
    it("removes 4/4:Tempo, adds 3/4:Tempo, and emits edit", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 1).trigger("click");

      const removeProp = findEditProp(editor, PropertyType.removedTags);
      const addProp = findEditProp(editor, PropertyType.addedTags);
      expect(removeProp?.value).toBe("4/4:Tempo");
      expect(addProp?.value).toBe("3/4:Tempo");
      expect(wrapper.emitted("edit")).toBeTruthy();
    });

    it("does NOT add 3/4:Tempo if it is already present on the song", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|3/4:Tempo|Slow Waltz:Dance",
        danceRatings: ["SWZ+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 1).trigger("click");

      // 3/4:Tempo should NOT be added again
      const addedProps = findAllEditProps(editor, PropertyType.addedTags);
      const addedThreeQuarter = addedProps.filter((p) => p.value === "3/4:Tempo");
      expect(addedThreeQuarter).toHaveLength(0);

      // 4/4:Tempo should still be removed
      const removeProp = findEditProp(editor, PropertyType.removedTags);
      expect(removeProp?.value).toBe("4/4:Tempo");
    });

    it("does NOT modify the tempo field", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 1).trigger("click");

      // No Tempo property in the edit history (setupEdit adds .Edit, User, Time — not Tempo)
      const tempoProps = findAllEditProps(editor, PropertyType.tempoField);
      expect(tempoProps).toHaveLength(0);
    });
  });

  describe("Case 3 — Bad meter + tempo (both meter and BPM are wrong)", () => {
    it("removes 4/4:Tempo, adds 3/4:Tempo, adjusts tempo to ¾ × original, and emits edit", async () => {
      // 120 BPM → 90 BPM after correction (120 × 3/4 = 90)
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 2).trigger("click");

      const removeProp = findEditProp(editor, PropertyType.removedTags);
      const addProp = findEditProp(editor, PropertyType.addedTags);
      const tempoProp = findEditProp(editor, PropertyType.tempoField);

      expect(removeProp?.value).toBe("4/4:Tempo");
      expect(addProp?.value).toBe("3/4:Tempo");
      expect(tempoProp?.value).toBe("90");
      expect(wrapper.emitted("edit")).toBeTruthy();
    });

    it("rounds the corrected tempo to the nearest integer", async () => {
      // 100 BPM → 75 BPM (100 × 3/4 = 75.0, no rounding needed)
      const history = makeHistory({
        tags: "4/4:Tempo|Slow Waltz:Dance",
        danceRatings: ["SWZ+1"],
        tempo: "100",
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 2).trigger("click");

      const tempoProp = findEditProp(editor, PropertyType.tempoField);
      expect(tempoProp?.value).toBe("75");
    });

    it("rounds fractional corrected tempo (e.g. 101 BPM → 76)", async () => {
      // 101 × 3/4 = 75.75 → rounds to 76
      const history = makeHistory({
        tags: "4/4:Tempo|Slow Waltz:Dance",
        danceRatings: ["SWZ+1"],
        tempo: "101",
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 2).trigger("click");

      const tempoProp = findEditProp(editor, PropertyType.tempoField);
      expect(tempoProp?.value).toBe("76");
    });

    it("marks the editor as modified", async () => {
      const { wrapper, editor } = mountCard();
      expect(editor.modified).toBe(false);
      await getButton(wrapper, 2).trigger("click");
      expect(editor.modified).toBe(true);
    });
  });

  describe("Case 4 — Compound time (4/4 feel with underlying waltz triple time)", () => {
    it("adds 12/8:Tempo song-level tag and Compound Time:Tempo dance tag, emits edit", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 3).trigger("click");

      const addedProps = findAllEditProps(editor, PropertyType.addedTags);
      const has128 = addedProps.some((p) => p.value === "12/8:Tempo");
      const compoundProp = findEditProp(editor, "Tag+:SWZ");

      expect(has128).toBe(true);
      expect(compoundProp?.value).toBe("Compound Time:Tempo");
      expect(wrapper.emitted("edit")).toBeTruthy();
    });

    it("does NOT add 12/8:Tempo if it is already present on the song", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|12/8:Tempo|Slow Waltz:Dance",
        danceRatings: ["SWZ+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 3).trigger("click");

      const addedProps = findAllEditProps(editor, PropertyType.addedTags);
      const added128 = addedProps.filter((p) => p.value === "12/8:Tempo");
      expect(added128).toHaveLength(0);

      // Compound Time tag still added to the waltz
      const compoundProp = findEditProp(editor, "Tag+:SWZ");
      expect(compoundProp?.value).toBe("Compound Time:Tempo");
    });

    it("adds Compound Time:Tempo to each waltz when multiple waltzes are present", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Slow Waltz:Dance|Viennese Waltz:Dance",
        danceRatings: ["SWZ+1", "VWZ+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 3).trigger("click");

      expect(findEditProp(editor, "Tag+:SWZ")?.value).toBe("Compound Time:Tempo");
      expect(findEditProp(editor, "Tag+:VWZ")?.value).toBe("Compound Time:Tempo");
    });

    it("adds Compound Time:Tempo for TGV (regression)", async () => {
      const history = makeHistory({
        tags: "4/4:Tempo|Tango Vals:Dance",
        danceRatings: ["TGV+1"],
      });
      const { wrapper, editor } = mountCard({ history });
      await getButton(wrapper, 3).trigger("click");

      expect(findEditProp(editor, "Tag+:TGV")?.value).toBe("Compound Time:Tempo");
    });

    it("does NOT remove or change the 4/4:Tempo tag", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 3).trigger("click");

      const removeProp = findEditProp(editor, PropertyType.removedTags);
      expect(removeProp).toBeUndefined();
    });

    it("does NOT modify the tempo BPM value", async () => {
      const { wrapper, editor } = mountCard();
      await getButton(wrapper, 3).trigger("click");

      const tempoProps = findAllEditProps(editor, PropertyType.tempoField);
      expect(tempoProps).toHaveLength(0);
    });
  });
});
