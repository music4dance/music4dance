import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe(
  "Custom Search",
  () => {
    test("renders a custom search page", () => {
      testPageSnapshot(App, model);
    }, 50000);
  },
);
