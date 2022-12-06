import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("DanceDetails.vue", () => {
  // This test works, but there are a bunch of annoying bootstrap-vue warnings
  test("renders a dance details page", () => {
    testPage(App, model);
  });
});
