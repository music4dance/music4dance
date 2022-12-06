import { m4dContext, testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";
import { model } from "./model";

describe("SongMerge.vue", () => {
  test("renders a song merge page", async () => {
    await testPageAsync(App, Preloads.All, model, m4dContext());
  });
});
