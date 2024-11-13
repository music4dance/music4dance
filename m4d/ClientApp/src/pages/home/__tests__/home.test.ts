import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("Home", () => {
  test("Renders the Home Page", () => {
    window.seedNumber = 2237892;
    testPageSnapshot(App, model);
  });
});
