import { describe, test } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Playlist Viewer", () => {
  test(
    "renders a playlist viewer index page",
    () => {
      testPageSnapshot(App, model);
    },
    50000,
  );
});
