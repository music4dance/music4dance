import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import FieldEditor from "../FieldEditor.vue";
import { MenuContext } from "@/models/MenuContext";

setupTestEnvironment();

const mockContext = new MenuContext();

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockContext,
  getAxiosXsrf: () => undefined,
}));

function setRoles(roles: string[]) {
  mockContext.roles = roles;
  mockContext.userName = roles.length ? "dwgray" : undefined;
}

function canEditWith(
  props: { role?: string; roles?: string[]; isCreator?: boolean },
  roles: string[],
) {
  setRoles(roles);
  const wrapper = mount(FieldEditor, {
    props: { name: "Artist", value: "Dolly Parton & Kenny Rogers", editing: true, ...props },
  });
  return wrapper.find("input").exists();
}

describe("FieldEditor permissions", () => {
  it("accepts any one of several roles", () => {
    const roles = ["dbAdmin", "canEdit"];
    expect(canEditWith({ roles }, ["canEdit"])).toBe(true);
    expect(canEditWith({ roles }, ["dbAdmin"])).toBe(true);
    expect(canEditWith({ roles }, ["canTag"])).toBe(false);
    expect(canEditWith({ roles }, [])).toBe(false);
  });

  it("still honours a single role", () => {
    expect(canEditWith({ role: "dbAdmin" }, ["dbAdmin"])).toBe(true);
    expect(canEditWith({ role: "dbAdmin" }, ["canEdit"])).toBe(false);
  });

  it("lets the song's creator edit whatever their roles", () => {
    expect(canEditWith({ role: "dbAdmin", isCreator: true }, [])).toBe(true);
  });

  it("denies everyone when no role is named", () => {
    expect(canEditWith({}, ["dbAdmin", "canEdit"])).toBe(false);
  });
});

describe("FieldEditor alternatives", () => {
  const mountWith = (props: Record<string, unknown>) => {
    setRoles(["dbAdmin"]);
    return mount(FieldEditor, {
      props: { name: "Title", value: "Song", role: "dbAdmin", editing: true, ...props },
    });
  };
  const offered = (wrapper: ReturnType<typeof mountWith>) =>
    wrapper.findAll("[data-alternative]").map((b) => b.text());

  it("offers each other value as a link", () => {
    const wrapper = mountWith({ alternatives: ["Song", "Song (Live)", "Song (Live)"] });
    expect(offered(wrapper)).toEqual(["Song (Live)"]);
  });

  it("compares numbers by value", () => {
    const wrapper = mountWith({
      name: "Tempo",
      value: "120",
      type: "number",
      alternatives: ["120.0", "124.0"],
    });
    expect(offered(wrapper)).toEqual(["124"]);
  });

  it("sets the field when a value is picked", async () => {
    const wrapper = mountWith({ alternatives: ["Song (Live)"] });
    await wrapper.find("[data-alternative]").trigger("click");
    const emitted = wrapper.emitted("update-field")!;
    expect(emitted).toHaveLength(1);
    expect(emitted[0]![0]).toMatchObject({ name: "Title", value: "Song (Live)" });
    expect((wrapper.find("input").element as HTMLInputElement).value).toBe("Song (Live)");
  });

  it("shows nothing when not editing", () => {
    const wrapper = mountWith({ editing: false, alternatives: ["Song (Live)"] });
    expect(offered(wrapper)).toEqual([]);
  });
});
