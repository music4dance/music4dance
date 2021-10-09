import { createLocalVue, mount } from "@vue/test-utils";
import BootstrapVue from "bootstrap-vue";
import { MenuContext } from "../../model/MenuContext";
import MainMenu from "../MainMenu.vue";

describe("MainMenu.vue", () => {
  test("renders the main menu with default options", () => {
    const localVue = createLocalVue();
    localVue.use(BootstrapVue);

    const context = new MenuContext();

    const wrapper = mount(MainMenu, {
      localVue,
      propsData: { context },
    });

    expect(wrapper).toMatchSnapshot();
  });

  test("renders the main menu with admin options", () => {
    const localVue = createLocalVue();
    localVue.use(BootstrapVue);
    const context: MenuContext = new MenuContext({
      userName: "dwgray",
      roles: ["dbAdmin"],
    });

    const wrapper = mount(MainMenu, {
      localVue,
      propsData: { context },
    });

    expect(wrapper).toMatchSnapshot();
  });
});
