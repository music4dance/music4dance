import { mount } from "@vue/test-utils";
import { expect, vi } from "vitest";
import { MenuContext } from "@/models/MenuContext";

// @ts-ignore
import { createBootstrap } from "bootstrap-vue-next";
import { setupTestEnvironment } from "./TestHelpers";

declare global {
  interface Window {
    menuContext: MenuContext;
    model_: unknown;
  }
}

setupTestEnvironment();

// vi.mock("@/helpers/GetMenuContext.ts", () => {
//   return {
//     getMenuContext: vi.fn(() => new MenuContext()),
//   };
// });

// vi.mock("@/helpers/TagEnvironmentManager.ts", () => {
//   return {
//     safeTagDatabase: vi.fn(() => loadTagsFromString(JSON.stringify(tagDatabaseJson))),
//   };
// });

// vi.mock("@/helpers/DanceEnvironmentManager.ts", () => {
//   return {
//     safeDanceDatabase: vi.fn(() => loadDancesFromString(loadTestDances())),
//   };
// });

let currentId = 1;

export function loadTestPage(app: unknown, model?: unknown, menuContext?: MenuContext) {
  const bsvn = createBootstrap({
    id: {
      getId: () => (currentId++).toString().padStart(4, "0"),
    },
  });

  if (model) {
    window.model_ = model;
  }

  if (menuContext) {
    window.menuContext = menuContext;
  }

  document.body.innerHTML = `
    <div>
        <h1>Non Vue app</h1>
        <div id="app"></div>
    </div>`;

  return mount(app, {
    attachTo: "#app",
    props: {},
    global: {
      stubs: { MainMenu: { template: "<span>MainMenu</span>" } },
      config: {},
      plugins: [bsvn],
    },
  });
}

// TODO: What is App? Can we type it more specifically? Does it actually have a provide function?
export function testPageSnapshot(app: unknown, model?: unknown, menuContext?: MenuContext): void {
  const wrapper = loadTestPage(app, model, menuContext);
  expect(wrapper.html()).toMatchSnapshot();
}
