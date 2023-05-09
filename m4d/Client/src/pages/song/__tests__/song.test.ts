import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

describe("Song.vue", () => {
  test("renders a song page", async () => {
    await testPageAsync(App, Preloads.All, model);
  });
});
