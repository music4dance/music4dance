import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestHelpers";
import { model } from "./model";
import App from "../App.vue";

describe("CompetitionCategory", () => {
  test("Renders the Ballroom Index Page", () => {
    testPageSnapshot(App, model);
  });
});
