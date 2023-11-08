import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import { MenuContext } from "@/models/MenuContext";

import MustRegister from "../MustRegister.vue";

describe("MustRegister.vue", () => {
  test("Checks the links", () => {
    const wrapper = mount(MustRegister, {
      props: { title: "Test", menuContext: new MenuContext() },
    });
    expect(wrapper.findAll("a").length).toBe(2);
  });

  test("Renders the MustRegister component", () => {
    const wrapper = mount(MustRegister, {
      props: { title: "Test", menuContext: new MenuContext() },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });
});
