import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Artist", () => {
  test(
    "renders an artist page",
    () => {
      testPageSnapshot(App, model);
    },
    50000,
  );
});
