import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("TempoList.vue", () => {
  test("renders the tempo list page", () => {
    testPage(App, model);
  });
});
