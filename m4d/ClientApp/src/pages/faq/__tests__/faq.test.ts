import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";

describe("Faq", () => {
  test("Renders Faq Page", () => {
    testPageSnapshot(App);
  });
});
