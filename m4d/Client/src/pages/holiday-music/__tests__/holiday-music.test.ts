import { Preloads } from "@/Preloads";
import { testPageAsync } from "@/helpers/TestHelpers";
import App from "../App.vue";
import { model } from "./model";

describe("HolidayMusic.vue", () => {
  test("renders a holiday music page", async () => {
    await testPageAsync(App, Preloads.Dances, model);
  });
});
