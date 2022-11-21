import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("Home.vue", () => {
  test("renders a competition category page", () => {
    window.seedNumber = 2237892;
    testPage(App, model);
  });
});
