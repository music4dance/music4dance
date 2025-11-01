import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Country", () => {
  test("Renders the Country Western Competition Page", () => {
    testPageSnapshot(App, model);
  });
});
