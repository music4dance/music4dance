import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";

describe("Resume", () => {
  test(
    "Renders Resume Page",
    () => {
      testPageSnapshot(App);
    },
    { timeout: 10000 },
  );
});
