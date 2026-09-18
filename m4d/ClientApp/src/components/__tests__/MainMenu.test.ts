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

  test.each([
    [true, true],
    [false, false],
  ])("Artists menu item follows the ArtistIndex flag (%s)", (artistIndex, expected) => {
    mockResizObserver();
    const context = new MenuContext();
    context.artistIndex = artistIndex;

    const AppWrapper = {
      name: "AppWrapper",
      render() {
        return h(BApp, null, { default: () => h(MainMenu, { context }) });
      },
    };

    const wrapper = mount(AppWrapper, { props: { context } });
    const artists = wrapper.findAll('a[href="/song/artists"]');
    expect(artists.length > 0).toBe(expected);
  });
});
