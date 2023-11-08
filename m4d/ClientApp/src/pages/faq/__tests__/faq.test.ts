import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("Faq", () => {
  test("Renders Faq Page", () => {
    testPageSnapshot(App);
  });
});
