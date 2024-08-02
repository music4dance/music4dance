import { loadTestDances } from "./LoadTestDances";
// @ts-ignore
import tagDatabaseJson from "@/assets/tags.json";
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
