import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("Resume", () => {
  test("Renders Resume Page", () => {
    testPageSnapshot(App);
  });
});
