import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { defineComponent } from "vue";
import SongCore from "../SongCore.vue";
import { SongDetailsModel } from "@/models/SongDetailsModel";
import { SongFilter } from "@/models/SongFilter";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import { MenuContext } from "@/models/MenuContext";

setupTestEnvironment();

let mockContext = new MenuContext({
  userName: "newuser",
  roles: [],
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
  props: { edit: Boolean },
  emits: ["edit", "update-song", "dance-vote", "delete-dance", "tag-clicked"],
  template: `<div />`,
});

const FieldEditorStub = defineComponent({
  name: "FieldEditor",
  props: ["name", "value", "editing", "isCreator", "role"],
  emits: ["update-field"],
  template: `<button class="field-editor" @click="$emit('update-field', { name, value: 'Updated' })">{{ name }}</button>`,
});

function makeCreatingModel(): SongDetailsModel {
  return new SongDetailsModel({
    created: true,
    userName: "newuser",
    filter: new SongFilter(),
    songHistory: new SongHistory({
      id: "00000000-0000-0000-0000-000000000002",
      properties: [
        new SongProperty({ name: ".Create", value: "" }),
        new SongProperty({ name: "User", value: "newuser" }),
        new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
        new SongProperty({ name: "Title", value: "New Song" }),
        new SongProperty({ name: "Artist", value: "New Artist" }),
      ],
    }),
  });
}

function mountCreating() {
  return mount(SongCore, {
    props: { model: makeCreatingModel(), startEditing: true, creating: true },
    global: {
      stubs: {
        DanceDetails: DanceDetailsStub,
        FieldEditor: FieldEditorStub,
        SongStats: true,
        TagListEditor: true,
        PageFrame: true,
        PurchaseSection: true,
        CommentEditor: true,
        DanceChooser: true,
        TagModal: true,
        SongLikeButton: true,
        BFormTextarea: true,
        BForm: true,
        AlbumList: true,
        TrackList: true,
        SongHistoryLog: true,
        SongHistoryViewer: true,
        WaltzCorrectionCard: true,
      },
    },
  });
}

describe("SongCore save gating for newly created songs", () => {
  it("hides the save button for an unprivileged user until a dance is voted on", async () => {
    mockContext = new MenuContext({
      userName: "newuser",
      roles: [],
      xsrfToken: "TEST_XSRF",
      searchHealthy: true,
    });
    const wrapper = mountCreating();

    await wrapper.find(".field-editor").trigger("click");
    await wrapper.vm.$nextTick();

    const saveButton = wrapper.findAll("button").find((b) => b.text() === "Add Song");
    expect(saveButton).toBeTruthy();
    expect(saveButton!.attributes("style")).toContain("display: none");

    const danceDetails = wrapper.findComponent({ name: "DanceDetails" });
    danceDetails.vm.$emit("dance-vote", new DanceRatingVote("CHA", VoteDirection.Up));
    await wrapper.vm.$nextTick();

    expect(saveButton!.attributes("style")).not.toContain("display: none");

    wrapper.unmount();
  });

  it("allows an admin to save a newly created song without voting on a dance", async () => {
    mockContext = new MenuContext({
      userName: "adminuser",
      roles: ["dbAdmin"],
      xsrfToken: "TEST_XSRF",
      searchHealthy: true,
    });
    const wrapper = mountCreating();

    await wrapper.find(".field-editor").trigger("click");
    await wrapper.vm.$nextTick();

    const saveButton = wrapper.findAll("button").find((b) => b.text() === "Add Song");
    expect(saveButton).toBeTruthy();
    expect(saveButton!.attributes("style") ?? "").not.toContain("display: none");

    wrapper.unmount();
  });
});
