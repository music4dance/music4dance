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
