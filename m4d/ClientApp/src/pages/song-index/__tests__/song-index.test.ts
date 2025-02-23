import { describe, expect, test } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Song Index", () => {
  test("checks the mounted component", () => {
    const wrapper = loadTestPage(App, model);
    expect(wrapper.find("#admin").exists).toBeTruthy();

    const t = document.getElementById("Title");
    expect(t).not.toBeNull();
  });

  test(
    "renders a song index page",
    () => {
      testPageSnapshot(App, model);
    },
    { timeout: 50000 },
  );
});
