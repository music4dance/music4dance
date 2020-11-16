import { createLocalVue, mount } from "@vue/test-utils";
import BootstrapVue from "bootstrap-vue";
import MainMenu from "../MainMenu.vue";
import { MenuContext } from "../../model/MenuContext";

describe("MainMenu.vue", () => {
  test("renders the main menu with default options", () => {
    const localVue = createLocalVue();
    localVue.use(BootstrapVue);

    const context: MenuContext = {};

    const wrapper = mount(MainMenu, {
      localVue,
      propsData: { context },
    });

    expect(wrapper).toMatchSnapshot();
  });

  test("renders the main menu with admin options", () => {
    const localVue = createLocalVue();
    localVue.use(BootstrapVue);
    const context: MenuContext = { userName: "dwgray", isAdmin: true };

    const wrapper = mount(MainMenu, {
      localVue,
      propsData: { context },
    });

    expect(wrapper).toMatchSnapshot();
  });
});
