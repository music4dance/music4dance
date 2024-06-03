import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

declare global {
  interface Window {
    seedNumber: number;
  }
}
describe("Home", () => {
  // INT-TODO: I think I must have broken the pseudo-random number generator
  test("Renders the Home Page", () => {
    window.seedNumber = 2237892;
    testPageSnapshot(App, model);
  });
});
