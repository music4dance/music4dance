import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";

describe("Tag Cloud", () => {
  test("Renders Tag Cloud Page", () => {
    testPageSnapshot(App);
  }, 100000);
});
