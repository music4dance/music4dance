import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { Song } from "@/models/Song";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { setupTestEnvironment, mockResizObserver } from "@/helpers/TestHelpers";
import SongStats from "../SongStats.vue";

setupTestEnvironment();
mockResizObserver();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Build a Song whose tempo was set only by an algorithmic/pseudo user.
 * isSystemTempo should be true: tempo exists but isUserModified("Tempo") == false.
 */
function makeAlgoTempoSong(tempo = "180"): Song {
  const history = new SongHistory({
    id: "test-song-id",
    properties: [
      new SongProperty({ name: ".Create", value: "" }),
      new SongProperty({ name: "User:Proxy", value: "batch-e|P" }),
      new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
      new SongProperty({ name: "Title", value: "Test Song" }),
      new SongProperty({ name: "Tempo", value: tempo }),
    ],
  });
  return Song.fromHistory(history);
}

/**
 * Build a Song whose tempo was set by a real (non-pseudo) user.
 * isSystemTempo should be false: tempo exists AND isUserModified("Tempo") == true.
 */
function makeHumanTempoSong(tempo = "180"): Song {
  const history = new SongHistory({
    id: "test-song-id",
    properties: [
      new SongProperty({ name: ".Create", value: "" }),
      new SongProperty({ name: "User", value: "dwgray" }),
      new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
      new SongProperty({ name: "Title", value: "Test Song" }),
      new SongProperty({ name: "Tempo", value: tempo }),
    ],
  });
  return Song.fromHistory(history);
}

/** Build a Song with no tempo set */
function makeNoTempoSong(): Song {
  const history = new SongHistory({
    id: "test-song-id",
    properties: [
      new SongProperty({ name: ".Create", value: "" }),
      new SongProperty({ name: "User", value: "dwgray" }),
      new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
      new SongProperty({ name: "Title", value: "Test Song" }),
    ],
  });
  return Song.fromHistory(history);
}

function mountStats(song: Song, user?: string, editing = false) {
  return mount(SongStats, {
    props: { song, user, editing },
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("SongStats.vue — canOverrideTempo / algo tempo pencil button", () => {
  describe("isSystemTempo detection", () => {
    it("is true when tempo was set only by an algorithmic (pseudo) user", () => {
      const song = makeAlgoTempoSong();
      expect(song.tempo).toBe(180);
      expect(song.isUserModified("Tempo")).toBe(false);
    });

    it("is false when tempo was set by a real user", () => {
      const song = makeHumanTempoSong();
      expect(song.tempo).toBe(180);
      expect(song.isUserModified("Tempo")).toBe(true);
    });

    it("is false when human overrides an algo-set tempo", () => {
      const history = new SongHistory({
        id: "test-song-id",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User:Proxy", value: "batch-e|P" }),
          new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
          new SongProperty({ name: "Tempo", value: "175" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "dwgray" }),
          new SongProperty({ name: "Time", value: "06/01/2020 12:00:00" }),
          new SongProperty({ name: "Tempo", value: "180" }),
        ],
      });
      const song = Song.fromHistory(history);
      expect(song.isUserModified("Tempo")).toBe(true);
    });
  });

  describe("pencil edit button visibility (canOverrideTempo)", () => {
    it("shows pencil button when user is authenticated AND tempo is algo-only", () => {
      const wrapper = mountStats(makeAlgoTempoSong(), "testuser");
      expect(wrapper.find("button").exists()).toBe(true);
    });

    it("hides pencil button when user is anonymous (no user prop)", () => {
      const wrapper = mountStats(makeAlgoTempoSong(), undefined);
      // The tempo row renders but without the override button
      expect(wrapper.find("button").exists()).toBe(false);
    });

    it("hides pencil button when tempo was set by a real user (human-modified)", () => {
      const wrapper = mountStats(makeHumanTempoSong(), "testuser");
      expect(wrapper.find("button").exists()).toBe(false);
    });

    it("hides pencil button when there is no tempo", () => {
      const wrapper = mountStats(makeNoTempoSong(), "testuser");
      expect(wrapper.find("button").exists()).toBe(false);
    });
  });

  describe("pencil button interaction", () => {
    it("emits 'edit' when the pencil button is clicked", async () => {
      const wrapper = mountStats(makeAlgoTempoSong(), "testuser");
      await wrapper.find("button").trigger("click");
      expect(wrapper.emitted("edit")).toBeTruthy();
      expect(wrapper.emitted("edit")).toHaveLength(1);
    });
  });

  describe("FieldEditor override-permission prop", () => {
    it("passes override-permission=true to FieldEditor when canOverrideTempo is true", () => {
      const wrapper = mountStats(makeAlgoTempoSong(), "testuser");
      const fieldEditors = wrapper.findAllComponents({ name: "FieldEditor" });
      const tempoEditor = fieldEditors.find((fe) => fe.props("name") === "Tempo");
      expect(tempoEditor).toBeDefined();
      expect(tempoEditor!.props("overridePermission")).toBe(true);
    });

    it("passes override-permission=false to FieldEditor when user is anonymous", () => {
      const wrapper = mountStats(makeAlgoTempoSong(), undefined);
      const fieldEditors = wrapper.findAllComponents({ name: "FieldEditor" });
      const tempoEditor = fieldEditors.find((fe) => fe.props("name") === "Tempo");
      expect(tempoEditor).toBeDefined();
      expect(tempoEditor!.props("overridePermission")).toBe(false);
    });

    it("passes override-permission=false to FieldEditor when tempo is human-modified", () => {
      const wrapper = mountStats(makeHumanTempoSong(), "testuser");
      const fieldEditors = wrapper.findAllComponents({ name: "FieldEditor" });
      const tempoEditor = fieldEditors.find((fe) => fe.props("name") === "Tempo");
      expect(tempoEditor).toBeDefined();
      expect(tempoEditor!.props("overridePermission")).toBe(false);
    });
  });
});
