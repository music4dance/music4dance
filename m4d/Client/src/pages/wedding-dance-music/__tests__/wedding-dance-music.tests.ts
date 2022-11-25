import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("WeddingDanceMusic.vue", () => {
  test("renders a wedding dance music page", () => {
    testPage(App, model);
  });
});
