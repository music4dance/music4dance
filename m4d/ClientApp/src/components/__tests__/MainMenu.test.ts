import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import { MenuContext } from "@/models/MenuContext";
import MainMenu from "../MainMenu.vue";
import { mockResizObserver } from "@/helpers/TestHelpers";
import { modalControllerPlugin, modalManagerPlugin } from "bootstrap-vue-next";

describe("MainMenu.vue", () => {
  test("Renders MainMenu for an anonymous user", () => {
    mockResizObserver();
    const context = new MenuContext();
    const wrapper = mount(MainMenu, {
      props: { context },
      global: { plugins: [modalControllerPlugin, modalManagerPlugin] },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });
});
