import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Album", () => {
  test("renders an album page", () => {
    testPageSnapshot(App, model);
  });
});
