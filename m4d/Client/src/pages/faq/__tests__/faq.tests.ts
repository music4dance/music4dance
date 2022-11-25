import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";

describe("Faq.vue", () => {
  test("renders the FAQ page", () => {
    testPage(App);
  });
});
