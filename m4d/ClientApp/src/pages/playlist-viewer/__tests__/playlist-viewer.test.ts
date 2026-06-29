import { afterAll, beforeAll, describe, test, vi } from "vitest";
import { testPageSnapshot } from "@/helpers/TestPageSnapshot";
import {
  model,
  limitedModelWithUnmatched,
  emptyPlaylistModel,
  noMatchesModel,
  anonymousUnmatchedModel,
} from "./model";
import App from "../App.vue";

describe("Playlist Viewer", () => {
  beforeAll(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-05-01T12:00:00"));
  });

  afterAll(() => {
    vi.useRealTimers();
  });

  test("renders a playlist viewer index page", () => {
    testPageSnapshot(App, model);
  }, 50000);

  test("renders the subscription upsell and unmatched-songs table", () => {
    testPageSnapshot(App, limitedModelWithUnmatched);
  }, 50000);

  test("renders a message for an empty playlist", () => {
    testPageSnapshot(App, emptyPlaylistModel);
  }, 50000);

  test("renders a message and add table when nothing matched", () => {
    testPageSnapshot(App, noMatchesModel);
  }, 50000);

  test("renders a sign-in call to action with returnUrl for anonymous viewers", () => {
    testPageSnapshot(App, anonymousUnmatchedModel);
  }, 50000);
});
