import { testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";

describe("DanceIndex.vue", () => {
  test("renders the dance index page", async () => {
    await testPageAsync(App, Preloads.Dances);
  });
});
