import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("CompetitionCategory", () => {
  test("Renders a Competition Category Page", () => {
    testPageSnapshot(App, model);
  });
});
