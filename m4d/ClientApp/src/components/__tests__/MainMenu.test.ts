import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import { MenuContext } from "@/models/MenuContext";
import MainMenu from "../MainMenu.vue";

describe("MainMenu.vue", () => {
  test("Renders MainMenu for an anonymous user", () => {
    const context = new MenuContext();
    const wrapper = mount(MainMenu, { props: { context } });
    expect(wrapper.html()).toMatchSnapshot();
  });
});
