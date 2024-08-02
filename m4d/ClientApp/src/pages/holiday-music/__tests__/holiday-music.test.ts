import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Holiday Music", () => {
  test("renders a holiday music page", () => {
    testPageSnapshot(App, model);
  });
});
