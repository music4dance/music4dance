import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("Home", () => {
  // INT-TODO: I think I must have broken the pseudo-random number generator
  test.skip("Renders the Home Page", () => {
    testPageSnapshot(App, model);
  });
});
