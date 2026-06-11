import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { defineComponent, nextTick } from "vue";
import SongCore from "../SongCore.vue";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { MenuContext } from "@/models/MenuContext";

setupTestEnvironment();

const mockContext = new MenuContext({
  userName: "dwgray",
  roles: ["canTag", "canEdit", "dbAdmin"],
  xsrfToken: "TEST_XSRF",
  searchHealthy: true,
});

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
}));

vi.mock("bootstrap-vue-next", () => ({
  useToast: () => ({ create: vi.fn() }),
  useModal: () => ({ create: vi.fn(() => ({ show: vi.fn(async () => false) })), hide: vi.fn() }),
}));

const DanceDetailsStub = defineComponent({
  name: "DanceDetails",
  props: {
    edit: Boolean,
  },
  emits: ["edit", "update-song", "dance-vote", "delete-dance", "tag-clicked"],
  template: `
    <div>
      <button id="dance-edit-button" @click="$emit('edit', 'input[data-edit-target=&quot;dance-tempo&quot;][data-dance-id=&quot;CHA&quot;]')">
        Dance Edit
      </button>
      <input
        v-if="edit"
        data-edit-target="dance-tempo"
        data-dance-id="CHA"
        type="number"
      />
    </div>
  `,
});

const SongStatsStub = defineComponent({
  name: "SongStats",
  emits: ["edit", "update-field"],
  template: `<div />`,
});

const TagListEditorStub = defineComponent({
  name: "TagListEditor",
  emits: ["edit", "update-song", "tag-clicked"],
  template: `<div data-edit-target="song-tags"><input type="text" /></div>`,
});

function makeModel(): SongDetailsModel {
  return new SongDetailsModel({
    userName: "dwgray",
    filter: new SongFilter(),
    songHistory: new SongHistory({
      id: "00000000-0000-0000-0000-000000000001",
      properties: [
        new SongProperty({ name: ".Create", value: "" }),
        new SongProperty({ name: "User", value: "dwgray" }),
        new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
        new SongProperty({ name: "Title", value: "Focus Test Song" }),
        new SongProperty({ name: "Artist", value: "Focus Artist" }),
        new SongProperty({ name: "DanceRating", value: "CHA+1" }),
      ],
    }),
  });
}

describe("SongCore selector propagation", () => {
  beforeEach(() => {
    vi.stubGlobal("requestAnimationFrame", (cb: FrameRequestCallback) => {
      cb(0);
      return 1;
    });
  });

  it("propagates dance tempo selector to SongCore focus lookup", async () => {
    const querySelectorSpy = vi.spyOn(document, "querySelector");
    const wrapper = mount(SongCore, {
      props: { model: makeModel() },
      global: {
        stubs: {
          DanceDetails: DanceDetailsStub,
          SongStats: SongStatsStub,
          TagListEditor: TagListEditorStub,
          PageFrame: true,
          PurchaseSection: true,
          CommentEditor: true,
          DanceChooser: true,
          TagModal: true,
          SongLikeButton: true,
          FieldEditor: true,
          BFormTextarea: true,
          BForm: true,
          AlbumList: true,
          TrackList: true,
          SongHistoryLog: true,
          SongHistoryViewer: true,
          WaltzCorrectionCard: true,
        },
      },
      attachTo: document.body,
    });

    const danceDetails = wrapper.findComponent({ name: "DanceDetails" });
    expect(danceDetails.exists()).toBe(true);

    danceDetails.vm.$emit("edit", 'input[data-edit-target="dance-tempo"][data-dance-id="CHA"]');
    await nextTick();
    await nextTick();

    const target = document.querySelector(
      'input[data-edit-target="dance-tempo"][data-dance-id="CHA"]',
    ) as HTMLInputElement | null;

    expect(target).not.toBeNull();
    expect(
      querySelectorSpy.mock.calls.some(
        (call) => call[0] === 'input[data-edit-target="dance-tempo"][data-dance-id="CHA"]',
      ),
    ).toBe(true);

    querySelectorSpy.mockRestore();

    wrapper.unmount();
  });
});
