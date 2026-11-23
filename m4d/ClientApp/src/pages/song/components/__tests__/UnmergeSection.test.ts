import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import UnmergeSection from "../UnmergeSection.vue";
import { MenuContext } from "@/models/MenuContext";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";

setupTestEnvironment();

const mockContext = new MenuContext();
vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
  getAxiosXsrf: () => undefined,
}));

const confirm = vi.fn();
vi.mock("bootstrap-vue-next", async (original) => ({
  ...(await original<typeof import("bootstrap-vue-next")>()),
  useModal: () => ({ create: () => ({ show: confirm }) }),
}));

const A = "aaaaaaaa-0000-0000-0000-000000000001";
const B = "bbbbbbbb-0000-0000-0000-000000000002";

const history = (props: [string, string][]) =>
  new SongHistory({
    id: "merged-id",
    properties: props.map(([name, value]) => new SongProperty({ name, value })),
  });

const merged = () =>
  history([
    [".Create", A],
    ["User", "alice"],
    ["Time", "01/01/2020 10:00:00 AM"],
    ["Title", "Song"],
    ["Artist", "Artist A"],
    [".Create", B],
    ["User", "bob"],
    ["Time", "01/02/2020 10:00:00 AM"],
    ["Title", "Song"],
    ["Artist", "Artist B"],
    [".Merge", `${A};${B}`],
    ["User", "dwgray"],
    ["Time", "01/05/2020 10:00:00 AM"],
  ]);

const mountSection = (songHistory: SongHistory, disabled = false) =>
  mount(UnmergeSection, { props: { history: songHistory, disabled } });

const button = (wrapper: ReturnType<typeof mountSection>) => wrapper.find("button[type=submit]");

describe("UnmergeSection", () => {
  let submit: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    confirm.mockReset();
    submit = vi.spyOn(HTMLFormElement.prototype, "submit").mockImplementation(() => {});
  });

  afterEach(() => {
    submit.mockRestore();
  });

  it("is hidden for a song that wasn't merged", () => {
    const wrapper = mountSection(
      history([
        [".Create", ""],
        ["User", "alice"],
      ]),
    );

    expect(wrapper.find("[data-test=unmerge-section]").exists()).toBe(false);
  });

  it("lists each source, posting the checked ids as extract", () => {
    const wrapper = mountSection(merged());

    const boxes = wrapper.findAll("input[name=extract]");
    expect(boxes.map((b) => (b.element as HTMLInputElement).value)).toEqual([A, B]);
    expect(wrapper.text()).toContain("Artist B");
    expect(wrapper.text()).toContain("added by bob");
    expect(wrapper.find("form").attributes("action")).toBe("/song/unmerge");
    expect((wrapper.find("input[name=id]").element as HTMLInputElement).value).toBe("merged-id");
    expect((wrapper.find("input[name=noMerge]").element as HTMLInputElement).value).toBe("true");
  });

  it("needs at least one source checked and one left", async () => {
    const wrapper = mountSection(merged());
    const boxes = wrapper.findAll("input[name=extract]");

    expect(button(wrapper).attributes("disabled")).toBeDefined();
    await boxes[1]!.setValue(true);
    expect(button(wrapper).attributes("disabled")).toBeUndefined();
    await boxes[0]!.setValue(true);
    expect(button(wrapper).attributes("disabled")).toBeDefined();
  });

  it("is disabled while the song is being edited", async () => {
    const wrapper = mountSection(merged(), true);

    await wrapper.findAll("input[name=extract]")[1]!.setValue(true);

    expect(button(wrapper).attributes("disabled")).toBeDefined();
  });

  it("submits only after the admin confirms", async () => {
    const wrapper = mountSection(merged());
    await wrapper.findAll("input[name=extract]")[1]!.setValue(true);

    confirm.mockResolvedValue({ ok: false });
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(submit).not.toHaveBeenCalled();

    confirm.mockResolvedValue({ ok: true });
    await wrapper.find("form").trigger("submit");
    await flushPromises();
    expect(submit).toHaveBeenCalledOnce();
  });
});
