import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("About", () => {
  test("Renders About Page", () => {
    testPageSnapshot(App);
  });
});
