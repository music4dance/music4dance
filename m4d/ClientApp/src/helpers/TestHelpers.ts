import { MenuContext } from "@/models/MenuContext";
import { loadTestDances } from "./LoadTestDances";
// @ts-ignore
import tagDatabaseJson from "@/assets/content/tags.json";
declare global {
  interface Window {
    danceDatabaseJson?: string;
    tagDatabaseJson?: string;
  }
}

export const setupTestEnvironment = () => {
  window.danceDatabaseJson = loadTestDances();
  window.tagDatabaseJson = JSON.stringify(tagDatabaseJson);
};

export const mockResizObserver = () => {
  // @ts-ignore
  window.ResizeObserver = class ResizeObserver {
    observe() {
      // do nothing
    }
    unobserve() {
      // do nothing
    }
    disconnect() {
      // do nothing
    }
  };
};

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
