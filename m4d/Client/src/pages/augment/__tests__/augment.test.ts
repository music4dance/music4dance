import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("Augment.vue", () => {
  test("renders the augment page", async () => {
    await testPageAsync(App, Preloads.All, {});
  });
});
