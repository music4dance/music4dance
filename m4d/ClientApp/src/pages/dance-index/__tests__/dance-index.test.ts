import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("DanceIndex", () => {
  test("Renders dance-index Page", () => {
    testPageSnapshot(App, model);
  });
});
