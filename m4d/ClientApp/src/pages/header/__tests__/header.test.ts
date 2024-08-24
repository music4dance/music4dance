import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";

describe("Header", () => {
  test("Renders Header Page", () => {
    testPageSnapshot(App);
  });
});
