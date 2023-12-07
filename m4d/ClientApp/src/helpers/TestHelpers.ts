import { loadTestDances } from "./LoadTestDances";
import { mount } from "@vue/test-utils";
import { expect, vi } from "vitest";
import { MenuContext } from "@/models/MenuContext";
import { loadTagsFromString } from "@/helpers/TagLoader";
import { loadDancesFromString } from "@/helpers/DanceLoader";
// @ts-ignore
import tagDatabaseJson from "@/assets/tags.json";
declare global {
  interface Window {
    model_: unknown;
  }
}

vi.mock("@/helpers/GetMenuContext.ts", () => {
  return {
    getMenuContext: vi.fn(() => new MenuContext()),
  };
});

vi.mock("@/helpers/TagEnvironmentManager.ts", () => {
  return {
    safeTagDatabase: vi.fn(() => loadTagsFromString(JSON.stringify(tagDatabaseJson))),
  };
});

vi.mock("@/helpers/DanceEnvironmentManager.ts", () => {
  return {
    safeDanceDatabase: vi.fn(() => loadDancesFromString(loadTestDances())),
  };
});

export function testPageSnapshot(App: any, model?: unknown): void {
  if (model) {
    window.model_ = model;
  }
  const wrapper = mount(App, {
    props: {},
    global: { stubs: { MainMenu: { template: "<span>MainMenu</span>" } } },
  });
  expect(wrapper.html()).toMatchSnapshot();
}
