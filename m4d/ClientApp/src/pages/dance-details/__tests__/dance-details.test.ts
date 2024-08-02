import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Dance Details", () => {
  test("Renders dance-details Page", () => {
    testPageSnapshot(App, model);
  });
});
