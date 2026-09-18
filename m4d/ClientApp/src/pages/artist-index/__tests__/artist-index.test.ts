import { describe, expect, test } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import { model } from "./model";
import App from "../App.vue";

describe("Artist Index", () => {
  test("renders an artist index page", () => {
    testPageSnapshot(App, model);
  }, 50000);

  test("tells visitors when the index is still being built", () => {
    const wrapper = loadTestPage(App, {
      ...model,
      letter: null,
      building: true,
      buckets: [],
      artists: [],
    });
    expect(wrapper.text()).toContain("putting the artist index together");
    expect(wrapper.find("ul.artist-list").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("No artists found");
  }, 50000);

  test("links artists, letters and the song-count toggle", () => {
    const wrapper = loadTestPage(App, model);
    expect(wrapper.findAll("ul.artist-list a").map((a) => a.attributes("href"))).toEqual([
      "/song/artist?name=The%20Beatles",
      "/song/artist?name=Michael%20Bubl%C3%A9",
    ]);
    expect(wrapper.text()).toContain("Artists: B");
    expect(wrapper.find('a[href="/song/artists?letter=D"]').exists()).toBe(true);
    expect(wrapper.find('a[href="/song/artists?letter=B&all=true"]').text()).toBe(
      "Include artists with only one song",
    );
  }, 50000);
});
