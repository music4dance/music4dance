import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Song Details", () => {
  test("Renders the Song Details Page", () => {
    testPageSnapshot(App, model);
  });
});
