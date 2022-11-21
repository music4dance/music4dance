import { loadPreloads } from "@/loadPreloads";
import { MenuContext } from "@/model/MenuContext";
import { Preloads } from "@/Preloads";
import { createLocalVue, mount } from "@vue/test-utils";
import { BootstrapVue, BootstrapVueIcons } from "bootstrap-vue";
import { VueConstructor } from "vue";
import VueTour from "vue-tour";

declare global {
  interface Window {
    menuContext: MenuContext;
    model: unknown;
  }
}

export function testPage(app: VueConstructor, model?: unknown): void {
  const localVue = createLocalVue();
  localVue.use(BootstrapVue);
  localVue.use(BootstrapVueIcons);
  localVue.use(VueTour);

  window.menuContext = new MenuContext({
    helpLink: "https://music4dance.blog/music4dance-help/song-list/",
    userName: "",
    userId: "",
    roles: [],
    indexId: "c",
    xsrfToken: "FOO",
  });

  if (model) {
    window.model = model;
  }
  const wrapper = mount(app, { localVue });
  expect(wrapper).toMatchSnapshot();
}

export async function testPageAsync(
  app: VueConstructor,
  preloads: Preloads,
  model?: unknown
): Promise<void> {
  await loadPreloads(preloads);

  const localVue = createLocalVue();
  localVue.use(BootstrapVue);
  localVue.use(BootstrapVueIcons);
  localVue.use(VueTour);

  window.menuContext = new MenuContext({
    helpLink: "https://music4dance.blog/music4dance-help/song-list/",
    userName: "",
    userId: "",
    roles: [],
    indexId: "c",
    xsrfToken: "FOO",
  });

  if (model) {
    window.model = model;
  }
  const wrapper = mount(app, { localVue });
  expect(wrapper).toMatchSnapshot();
}
