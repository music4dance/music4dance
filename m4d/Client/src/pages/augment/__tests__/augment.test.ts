import { testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";

describe("Augment.vue", () => {
  test("renders the augment page", async () => {
    await testPageAsync(App, Preloads.All, {});
  });
});
