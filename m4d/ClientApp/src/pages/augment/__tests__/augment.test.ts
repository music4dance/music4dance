import { describe, test, expect, beforeEach, vi } from "vitest";
import { mount, VueWrapper } from "@vue/test-utils";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongHistory } from "@/models/SongHistory";

// Mock the global model_ variable
declare global {
  // eslint-disable-next-line no-var
  var model_: string;
}

// Mock getMenuContext
vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => ({
    userName: "testuser",
    isAdmin: false,
  }),
}));

// Mock components that we don't need to test
vi.mock("@/components/PageFrame.vue", () => ({
  default: {
    name: "PageFrame",
    template: "<div><slot /></div>",
  },
}));

vi.mock("../components/AugmentSearch.vue", () => ({
  default: {
    name: "AugmentSearch",
    template: "<div>AugmentSearch</div>",
    emits: ["editSong"],
  },
}));

vi.mock("../components/AugmentLookup.vue", () => ({
  default: {
    name: "AugmentLookup",
    template: "<div>AugmentLookup</div>",
    emits: ["editSong"],
  },
}));

vi.mock("../components/AugmentInfo.vue", () => ({
  default: {
    name: "AugmentInfo",
    template: "<div>AugmentInfo</div>",
  },
}));

vi.mock("../../song/components/SongCore.vue", () => ({
  default: {
    name: "SongCore",
    template: "<div>SongCore</div>",
    emits: ["song-saved", "cancel-changes"],
  },
}));

describe("Augment Page", () => {
  beforeEach(() => {
    // Set up minimal global model_
    globalThis.model_ = "{}";
  });

  test("Renders the Augment Page", () => {
    testPageSnapshot(App, {});
  });

  describe("Page State Management", () => {
    let wrapper: VueWrapper<InstanceType<typeof App>>;

    beforeEach(() => {
      globalThis.model_ = "{}";
      wrapper = mount(App, {
        global: {
          stubs: {
            PageFrame: true,
            AugmentSearch: true,
            AugmentLookup: true,
            AugmentInfo: true,
            SongCore: true,
            BRow: true,
            BCol: true,
            BAlert: true,
            BTabs: true,
            BTab: true,
            BCardText: true,
            BInputGroup: true,
            BFormInput: true,
            BButton: true,
          },
        },
      });
    });

    test("Initial state - should be in lookup phase", () => {
      expect(wrapper.vm.phase).toBe("lookup");
      expect(wrapper.vm.songModel).toBeNull();
      expect(wrapper.vm.lastSong).toBeNull();
      expect(wrapper.vm.created).toBe(false);
      expect(wrapper.vm.tabIndex).toBe(0);
    });

    test("editSong - should change to edit phase and set songModel", () => {
      const mockHistory = SongHistory.fromString(
        ".Create=\tTitle=Test Song\tArtist=Test Artist\tTempo=120",
      );
      const testModel = new SongDetailsModel({
        created: true,
        songHistory: mockHistory,
      });

      wrapper.vm.editSong(testModel);

      expect(wrapper.vm.phase).toBe("edit");
      expect(wrapper.vm.songModel).not.toBeNull();
      expect(wrapper.vm.songModel?.created).toBe(true);
    });

    test("reset with saved=true - should set lastSong and return to lookup phase", async () => {
      // First, set up the edit state
      const mockHistory = SongHistory.fromString(
        ".Create=\tTitle=Test Song\tArtist=Test Artist\tTempo=120",
      );
      const testModel = new SongDetailsModel({
        created: true,
        songHistory: mockHistory,
      });

      wrapper.vm.editSong(testModel);
      expect(wrapper.vm.phase).toBe("edit");

      // Set tabIndex to simulate being on "by Id" tab
      wrapper.vm.tabIndex = 1;

      // Now reset with saved=true
      wrapper.vm.reset(true);

      expect(wrapper.vm.phase).toBe("lookup");
      expect(wrapper.vm.songModel).toBeNull();
      expect(wrapper.vm.lastSong).not.toBeNull();
      expect(wrapper.vm.lastSong?.title).toBe("Test Song");
      expect(wrapper.vm.created).toBe(true);
      expect(wrapper.vm.tabIndex).toBe(0); // Should reset to first tab
    });

    test("reset with saved=false - should return to lookup without setting lastSong", async () => {
      // First, set up the edit state
      const mockHistory = SongHistory.fromString(
        ".Create=\tTitle=Test Song\tArtist=Test Artist\tTempo=120",
      );
      const testModel = new SongDetailsModel({
        created: true,
        songHistory: mockHistory,
      });

      wrapper.vm.editSong(testModel);
      expect(wrapper.vm.phase).toBe("edit");

      // Set tabIndex to simulate being on "by Id" tab
      wrapper.vm.tabIndex = 1;

      // Now reset with saved=false (cancel)
      wrapper.vm.reset(false);

      expect(wrapper.vm.phase).toBe("lookup");
      expect(wrapper.vm.songModel).toBeNull();
      expect(wrapper.vm.lastSong).toBeNull(); // Should NOT set lastSong when cancelled
      expect(wrapper.vm.tabIndex).toBe(0); // Should still reset tab
    });

    test("Initial load with ID - should set tabIndex to 1", async () => {
      // Set up model with an ID
      globalThis.model_ = JSON.stringify({ id: "spotify-1234" });

      const wrapperWithId = mount(App, {
        global: {
          stubs: {
            PageFrame: true,
            AugmentSearch: true,
            AugmentLookup: true,
            AugmentInfo: true,
            SongCore: true,
            BRow: true,
            BCol: true,
            BAlert: true,
            BTabs: true,
            BTab: true,
            BCardText: true,
            BInputGroup: true,
            BFormInput: true,
            BButton: true,
          },
        },
      });

      // Wait for onMounted to complete
      await wrapperWithId.vm.$nextTick();
      expect(wrapperWithId.vm.tabIndex).toBe(1);
    });
  });
});
