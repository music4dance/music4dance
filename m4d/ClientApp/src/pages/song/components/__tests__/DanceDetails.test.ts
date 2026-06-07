import { describe, it, expect, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { BCard, BListGroup, BListGroupItem, BButton, BCloseButton } from "bootstrap-vue-next";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import DanceDetails from "../DanceDetails.vue";
import { Song } from "@/models/Song";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { MenuContext } from "@/models/MenuContext";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Mock getMenuContext so we can control roles per-test
// ---------------------------------------------------------------------------

const mockContext = new MenuContext();

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
  getAxiosXsrf: () => undefined,
}));

function setRoles(roles: string[]) {
  mockContext.roles = roles;
  mockContext.userName = roles.length ? "dwgray" : undefined;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeHistory(songTempo?: number, chaTempo?: number): SongHistory {
  const props: SongProperty[] = [
    new SongProperty({ name: ".Create", value: "" }),
    new SongProperty({ name: "User", value: "dwgray" }),
    new SongProperty({ name: "Time", value: "01/01/2024 12:00:00 PM" }),
    new SongProperty({ name: "DanceRating", value: "CHA+1" }),
  ];
  if (songTempo != null) props.push(new SongProperty({ name: "Tempo", value: String(songTempo) }));
  if (chaTempo != null)
    props.push(new SongProperty({ name: "Tempo:CHA", value: String(chaTempo) }));
  return new SongHistory({ id: "00000000-0000-0000-0000-000000000001", properties: props });
}

function makeSong(songTempo?: number, chaTempo?: number): Song {
  return Song.fromHistory(makeHistory(songTempo, chaTempo));
}

const STUBS = {
  DanceVote: true,
  DanceName: true,
  TagListEditor: true,
  CommentEditor: true,
  BCloseButton: true,
  IBiXCircle: { template: "<span class='x-circle-stub' />" },
  IBiLink45deg: { template: "<span class='link-stub' />" },
  IBiExclamationCircle: true,
  IBiX: true,
};

function mountDetails(
  song: Song,
  { edit = false, user, editor }: { edit?: boolean; user?: string; editor?: SongEditor } = {},
) {
  return mount(DanceDetails, {
    props: {
      song,
      title: "Dances",
      danceRatings: song.danceRatings ?? [],
      filter: new SongFilter(),
      user,
      editor: editor ?? (user ? new SongEditor(undefined, user, makeHistory()) : undefined),
      edit,
    },
    global: {
      stubs: STUBS,
      components: { BCard, BListGroup, BListGroupItem, BButton },
    },
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

afterEach(() => {
  mockContext.roles = [];
  mockContext.userName = undefined;
});

describe("DanceDetails.vue — tempo display (all users)", () => {
  it("shows inherited song tempo for anonymous users", () => {
    setRoles([]);
    const song = makeSong(120);
    const wrapper = mountDetails(song);
    expect(wrapper.text()).toContain("120");
    expect(wrapper.text()).toContain("BPM");
  });

  it("shows nothing when no tempo is set", () => {
    setRoles([]);
    const song = makeSong();
    const wrapper = mountDetails(song);
    expect(wrapper.text()).not.toContain("BPM");
  });

  it("shows per-dance override value when set", () => {
    setRoles([]);
    const song = makeSong(120, 128);
    const wrapper = mountDetails(song);
    expect(wrapper.text()).toContain("128");
    expect(wrapper.text()).toContain("BPM");
  });

  it("shows promoted song tempo when only dance tempo is set", () => {
    setRoles([]);
    const song = makeSong(undefined, 128);
    const wrapper = mountDetails(song);
    expect(wrapper.text()).toContain("128");
    expect(wrapper.text()).toContain("BPM");
  });

  it("shows link icon for inherited tempo (no override)", () => {
    setRoles([]);
    const song = makeSong(120);
    const wrapper = mountDetails(song);
    // IBiLink45deg renders as inline SVG in view mode when tempo is inherited
    const viewSpan = wrapper.find("span.ms-2.small");
    expect(viewSpan.find("svg").exists()).toBe(true);
  });

  it("shows link icon in edit mode when tempo is inherited", () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    // Link icon appears in the label span even in edit mode when no override
    const labelSpan = wrapper.find("span.text-muted");
    expect(labelSpan.find("svg").exists()).toBe(true);
  });

  it("uses success (green) color for label when override is active", () => {
    setRoles(["canTag"]);
    const song = makeSong(120, 128);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    expect(wrapper.find("span.text-success").exists()).toBe(true);
    expect(wrapper.find("span.text-warning").exists()).toBe(false);
  });

  it("does not show link icon when override is active", () => {
    setRoles([]);
    const song = makeSong(120, 128);
    const wrapper = mountDetails(song);
    // Override is active: view span shows text only, no SVG icon
    const viewSpan = wrapper.find("span.ms-2.small");
    expect(viewSpan.find("svg").exists()).toBe(false);
  });
});

describe("DanceDetails.vue — tempo editing (canTag required)", () => {
  it("shows edit input when canTag + edit mode", () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    expect(wrapper.find('input[type="number"]').exists()).toBe(true);
  });

  it("edit input has empty value and placeholder = song tempo when no override", () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    const input = wrapper.find('input[type="number"]');
    expect(input.attributes("placeholder")).toBe("120");
    expect((input.element as HTMLInputElement).value).toBe("");
  });

  it("edit input shows override value when override is set", () => {
    setRoles(["canTag"]);
    const song = makeSong(120, 128);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    const input = wrapper.find('input[type="number"]');
    expect((input.element as HTMLInputElement).value).toBe("128");
  });

  it("does NOT show edit input without canTag role", () => {
    setRoles(["canEdit"]);
    mockContext.userName = "bob";
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: true, user: "bob" });
    expect(wrapper.find('input[type="number"]').exists()).toBe(false);
    expect(wrapper.text()).toContain("120"); // view mode still shows tempo
  });

  it("does NOT show edit input when not in edit mode", () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: false, user: "dwgray" });
    expect(wrapper.find('input[type="number"]').exists()).toBe(false);
    expect(wrapper.text()).toContain("120"); // view mode still shows tempo
  });

  it("shows clear button when override is active in edit mode", () => {
    setRoles(["canTag"]);
    const song = makeSong(120, 128);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    // The clear button has title="Clear per-dance override..."
    const clearBtn = wrapper.find('button[title^="Clear per-dance"]');
    expect(clearBtn.exists()).toBe(true);
  });

  it("does NOT show clear button when no override is active", () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const wrapper = mountDetails(song, { edit: true, user: "dwgray" });
    expect(wrapper.find('button[title^="Clear per-dance"]').exists()).toBe(false);
  });

  it("emits edit and updates editor when input value changes", async () => {
    setRoles(["canTag"]);
    const song = makeSong(120);
    const editor = new SongEditor(undefined, "dwgray", makeHistory(120));
    const wrapper = mountDetails(song, { edit: true, user: "dwgray", editor });

    const input = wrapper.find('input[type="number"]');
    await input.setValue("132");
    await input.trigger("blur");

    expect(wrapper.emitted("edit")).toBeTruthy();
    const tempoProps = editor.editHistory.properties.filter((p) => p.name === "Tempo:CHA");
    expect(tempoProps).toHaveLength(1);
    expect(tempoProps[0].value).toBe("132");
  });
});
