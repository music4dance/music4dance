import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("About.vue", () => {
  test("renders the about page", () => {
    testPage(App);
  });
});
