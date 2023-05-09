import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

describe("Album.vue", () => {
  test("renders an album page", async () => {
    await testPageAsync(App, Preloads.Dances, model);
  });
});
