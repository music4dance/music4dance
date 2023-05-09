import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("AdvancedSearch.vue", () => {
  const originalWindowLocation = window.location;

  beforeEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: new URL(
        "https://localhost:5001/song/advancedsearchform?filter=v2-Advanced-BOL%2CRMB--Love"
      ),
    });
  });

  afterEach(() => {
    Object.defineProperty(window, "location", {
      configurable: true,
      enumerable: true,
      value: originalWindowLocation,
    });
  });

  test("renders the advanced search page", async () => {
    await testPageAsync(App, Preloads.All);
  });
});
