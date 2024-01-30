import { loadPreloads } from "@/loadPreloads";
import { MenuContext } from "@/model/MenuContext";
import { Preloads } from "@/Preloads";
import { createLocalVue, mount } from "@vue/test-utils";
import { BootstrapVue, BootstrapVueIcons } from "bootstrap-vue";
import { VueConstructor } from "vue";
import VueShowdown from "vue-showdown";

declare global {
  interface Window {
    menuContext: MenuContext;
    model: unknown;
  }
}

export function testPage(app: VueConstructor, model?: unknown): void {
  supressWarning();
  fakeDate();
  window.menuContext = anonymousContext();

  const localVue = setupVue();

  if (model) {
    window.model = model;
  }
  const wrapper = mount(app, { localVue });
  expect(wrapper).toMatchSnapshot();
}

export async function testPageAsync(
  app: VueConstructor,
  preloads: Preloads,
  model?: unknown,
  menuContext?: MenuContext
): Promise<void> {
  supressWarning();
  fakeDate();

  await loadPreloads(preloads);

  window.menuContext = menuContext ?? anonymousContext();

  const localVue = setupVue();

  if (model) {
    window.model = model;
  }
  const wrapper = mount(app, { localVue });
  expect(wrapper).toMatchSnapshot();
}

export function anonymousContext(): MenuContext {
  return new MenuContext({
    helpLink: "https://music4dance.blog/music4dance-help/song-list/",
    userName: "",
    userId: "",
    roles: [],
    indexId: "c",
    xsrfToken: "FOO",
  });
}

export function m4dContext(): MenuContext {
  return new MenuContext({
    helpLink: "https://music4dance.blog/music4dance-help/song/",
    userName: "music4dance",
    userId: "",
    roles: ["showDiagnostics", "canEdit", "dbAdmin", "canTag", "premium"],
    indexId: "c",
    updateMessage: "",
    xsrfToken: "FOO",
  });
}

function setupVue(): VueConstructor {
  const localVue = createLocalVue();
  localVue.use(BootstrapVue);
  localVue.use(BootstrapVueIcons);
  localVue.use(VueShowdown, { flavor: "vanilla" });

  return localVue;
}

function fakeDate(): void {
  jest.useFakeTimers().setSystemTime(new Date("2022-02-07").getTime());
}

let warningTrapped = false;
function supressWarning(): void {
  if (warningTrapped) {
    return;
  }
  // eslint-disable-next-line no-console
  const originalWarn = console.warn.bind(console);

  // eslint-disable-next-line no-console
  console.warn = (...args) => {
    const s = args.toString();
    const bad = [
      "[BootstrapVue warn]: tooltip - The provided target is no valid HTML element",
      "[BootstrapVue warn]: tooltip - Unable to find target element by ID",
    ];
    return !bad.some((b) => s.includes(b)) && originalWarn(...args);
  };

  warningTrapped = true;
}
