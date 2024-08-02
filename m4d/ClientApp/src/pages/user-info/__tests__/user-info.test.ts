import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { model } from "./model";

describe("User Info", () => {
  test("Renders User Info Page", () => {
    testPageSnapshot(App, model);
  });
});
