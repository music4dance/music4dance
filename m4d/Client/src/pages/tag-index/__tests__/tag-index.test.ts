import { testPageAsync } from "@/helpers/TestHelpers";
import { Preloads } from "@/Preloads";
import App from "../App.vue";

describe("TagIndex.vue", () => {
  test("renders the tag index page", async () => {
    await testPageAsync(App, Preloads.Tags);
  });
});
