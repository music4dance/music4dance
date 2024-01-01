import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("Home", () => {
  test("Renders the Home Page", () => {
    testPageSnapshot(App, model);
  });
});
