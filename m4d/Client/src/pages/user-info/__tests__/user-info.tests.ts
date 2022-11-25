import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

declare global {
  interface Window {
    seedNumber: number;
  }
}

describe("UserInfo.vue", () => {
  test("renders a user info page", () => {
    testPage(App, model);
  });
});
