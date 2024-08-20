import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";

describe("Augment Page", () => {
  test("Renders the Augment Page", () => {
    testPageSnapshot(App, {});
  });
});
