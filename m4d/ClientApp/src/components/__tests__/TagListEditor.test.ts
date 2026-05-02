import { describe, it, expect, vi, beforeEach } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";
import { SongFilter } from "@/models/SongFilter";
import { MenuContext } from "@/models/MenuContext";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import TagListEditor from "../TagListEditor.vue";

setupTestEnvironment();

// ---------------------------------------------------------------------------
// Mock getMenuContext so individual tests can control canEdit
// ---------------------------------------------------------------------------

const mockMenuContext = new MenuContext();

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: vi.fn(() => mockMenuContext),
}));

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build a minimal SongHistory with a system (batch) tag */
function makeHistoryWithSystemTag(tag = "Pop:Music"): SongHistory {
  return new SongHistory({
    id: "test-song-id",
    properties: [
      new SongProperty({ name: ".Create", value: "" }),
      new SongProperty({ name: "User:Proxy", value: "batch-s|P" }),
      new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
      new SongProperty({ name: "Title", value: "Test Song" }),
      new SongProperty({ name: "Tag+", value: tag }),
    ],
  });
}

/** Build a minimal SongHistory with a human-added tag */
function makeHistoryWithHumanTag(tag = "Pop:Music"): SongHistory {
  return new SongHistory({
    id: "test-song-id",
    properties: [
      new SongProperty({ name: ".Create", value: "" }),
      new SongProperty({ name: "User", value: "dwgray" }),
      new SongProperty({ name: "Time", value: "01/01/2020 12:00:00" }),
      new SongProperty({ name: "Title", value: "Test Song" }),
      new SongProperty({ name: "Tag+", value: tag }),
    ],
  });
}

function makeSong(history: SongHistory, user = "testuser"): Song {
  return Song.fromHistory(history, user);
}

function mountEditor(opts: {
  history: SongHistory;
  user?: string;
  edit?: boolean;
  canEdit?: boolean;
  systemTagKeys?: Set<string>;
}) {
  const { history, user = "testuser", edit = true, canEdit = false, systemTagKeys } = opts;

  // Configure the mock context
  mockMenuContext.roles = canEdit ? ["canEdit"] : [];

  const song = makeSong(history, user);
  const editor = user ? new SongEditor(undefined, user, history) : undefined;
  const filter = new SongFilter();

  return shallowMount(TagListEditor, {
    props: {
      container: song,
      filter,
      user,
      editor,
      edit,
      systemTagKeys,
    },
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("TagListEditor.vue — system tag removal permissions", () => {
  beforeEach(() => {
    mockMenuContext.roles = [];
  });

  describe("authenticated user without canEdit role", () => {
    it("sees 'Remove System Tags' section when systemTagKeys is non-empty and has matching tags", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const systemTagKeys = new Set(["Pop:Music"]);
      const wrapper = mountEditor({ history, systemTagKeys, canEdit: false });
      expect(wrapper.html()).toContain("Remove System Tags");
    });

    it("does NOT see 'Remove Tags' section (that requires canEdit)", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const systemTagKeys = new Set(["Pop:Music"]);
      const wrapper = mountEditor({ history, systemTagKeys, canEdit: false });
      // "Remove Tags:" (without "System") should not appear
      const html = wrapper.html();
      expect(html).not.toMatch(/Remove Tags:(?!.*System)/);
    });

    it("does NOT see 'Remove System Tags' when systemTagKeys is empty", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const wrapper = mountEditor({ history, systemTagKeys: new Set(), canEdit: false });
      expect(wrapper.html()).not.toContain("Remove System Tags");
    });

    it("does NOT see 'Remove System Tags' when systemTagKeys is undefined", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const wrapper = mountEditor({ history, systemTagKeys: undefined, canEdit: false });
      expect(wrapper.html()).not.toContain("Remove System Tags");
    });

    it("does NOT see 'Remove System Tags' when tag is human-added (not in systemTagKeys)", () => {
      // Tag was added by a human, so it's NOT in systemTagKeys
      const history = makeHistoryWithHumanTag("Pop:Music");
      const wrapper = mountEditor({
        history,
        systemTagKeys: new Set(), // empty — no system tags
        canEdit: false,
      });
      expect(wrapper.html()).not.toContain("Remove System Tags");
    });
  });

  describe("canEdit user", () => {
    it("sees 'Remove Tags:' section for ALL others' tags (not just system)", () => {
      const history = makeHistoryWithHumanTag("Pop:Music");
      const wrapper = mountEditor({ history, canEdit: true });
      expect(wrapper.html()).toContain("Remove Tags:");
    });

    it("does NOT see 'Remove System Tags' (gets the more powerful section instead)", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const systemTagKeys = new Set(["Pop:Music"]);
      const wrapper = mountEditor({ history, systemTagKeys, canEdit: true });
      expect(wrapper.html()).not.toContain("Remove System Tags");
    });
  });

  describe("anonymous user (no user prop)", () => {
    it("sees neither 'Remove Tags' nor 'Remove System Tags'", () => {
      const history = makeHistoryWithSystemTag("Pop:Music");
      const systemTagKeys = new Set(["Pop:Music"]);
      const wrapper = mountEditor({
        history,
        user: undefined as unknown as string,
        systemTagKeys,
        canEdit: false,
      });
      const html = wrapper.html();
      expect(html).not.toContain("Remove Tags");
    });
  });
});
