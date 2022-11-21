import { testPage } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

describe("CompetitionCategory.vue", () => {
  test("renders a competition category page", () => {
    testPage(App, model);
  });
});
