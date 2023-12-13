import { describe, expect, test } from "vitest";
import { shallowMount } from "@vue/test-utils";
import LinkCategory from "../LinkCategory.vue";

describe("LinkCategory.vue", () => {
  test("renders name a a link when passed", () => {
    const name = "The Name";
    const wrapper = shallowMount(LinkCategory, {
      propsData: { name },
    });
    expect(wrapper.text()).toMatch(name);

    const link = wrapper.find("a");
    expect(link.exists()).toBe(true);
    expect(link.attributes("href")).toBe("/dances/the-name");
  });
});
