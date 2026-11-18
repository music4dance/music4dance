import { describe, expect, test } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Artist", () => {
  test("renders an artist page", () => {
    testPageSnapshot(App, model);
  }, 50000);

  test("encodes the artist in its search links", () => {
    // An unencoded & ended the query string, leaving `Artist:(Coco with no closing paren,
    // which Azure Search rejects
    const wrapper = loadTestPage(App, { ...model, artist: "Coco & Breezy" });
    const hrefs = wrapper
      .findAll("a")
      .map((a) => a.attributes("href") ?? "")
      .filter((href) => href.startsWith("/song/filtersearch?filter="));

    expect(hrefs.length).toBeGreaterThan(0);
    hrefs.forEach((href) => {
      const filter = new URLSearchParams(href.split("?")[1]).get("filter");
      expect(filter).toContain("`Artist:(Coco & Breezy)");
    });
  }, 50000);
});
