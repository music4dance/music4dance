import { testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";

describe("TempoCounter.vue", () => {
  test("renders the tempo counter page", async () => {
    await testPageAsync(App, Preloads.Dances, { numerator: 4, tempo: 100 });
  });
});
