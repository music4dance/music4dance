import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("DanceIndex.vue", () => {
  test("renders the dance index page", async () => {
    await testPageAsync(App, Preloads.Dances);
  });
});
