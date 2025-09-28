import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import { MenuContext } from "@/models/MenuContext";
import MainMenu from "../MainMenu.vue";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { BApp } from "bootstrap-vue-next";
import { h } from "vue";

describe("MainMenu.vue", () => {
  test("Renders MainMenu for an anonymous user", () => {
    mockResizObserver();
    const context = new MenuContext();

    const AppWrapper = {
      name: "AppWrapper",
      render() {
        return h(BApp, null, { default: () => h(MainMenu, { context }) });
      },
    };

    const wrapper = mount(AppWrapper, {
      props: { context },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });
});
