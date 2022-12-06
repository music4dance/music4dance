import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("Resume.vue", () => {
  test("renders the resume page", () => {
    testPage(App);
  });
});
