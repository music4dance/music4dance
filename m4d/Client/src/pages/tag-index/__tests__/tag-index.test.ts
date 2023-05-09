import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("TagIndex.vue", () => {
  test("renders the tag index page", async () => {
    await testPageAsync(App, Preloads.Tags);
  });
});
