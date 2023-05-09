import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

describe("NewMusic.vue", () => {
  test("renders a new music page", async () => {
    await testPageAsync(App, Preloads.Dances, model);
  });
});
