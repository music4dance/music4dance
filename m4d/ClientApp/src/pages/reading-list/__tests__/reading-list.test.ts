import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("About", () => {
  // INT-TODO: Need stable IDs for tab buttons
  test("Render the Readling List Page", () => {
    testPageSnapshot(App);
  });
});
