import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("New Music", () => {
  test("renders a new music page", () => {
    testPageSnapshot(App, model);
  });
});
