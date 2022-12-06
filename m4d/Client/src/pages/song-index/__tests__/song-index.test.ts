import { testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";
import { model } from "./model";

describe("SongIndex.vue", () => {
  test("renders a song index page", async () => {
    await testPageAsync(App, Preloads.Dances, model);
  });
});
