import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import ArtistCredit from "../ArtistCredit.vue";
import { Song } from "@/models/Song";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";

setupTestEnvironment();

function song(log: string[]): Song {
  const properties = log.map((entry) => {
    const eq = entry.indexOf("=");
    return new SongProperty({ name: entry.slice(0, eq), value: entry.slice(eq + 1) });
  });
  return Song.fromHistory(new SongHistory({ id: "s1", properties }));
}

const duet = song([
  ".Create=",
  "User=dwgray",
  "Time=01/15/2024 14:30:00",
  "Title=Islands in the Stream",
  "Artist=Dolly Parton & Kenny Rogers",
  ".Edit=",
  "User=artist-bot|P",
  "Time=01/16/2024 14:30:00",
  "Artists=Dolly Parton|Kenny Rogers",
]);

const featured = song([
  ".Create=",
  "User=dwgray",
  "Time=01/15/2024 14:30:00",
  "Title=Hold My Heart (feat. ZZ Ward)",
  "Artist=Lindsey Stirling",
  "Artists=Lindsey Stirling|ZZ Ward",
]);

describe("ArtistCredit.vue", () => {
  it("links the whole credit when individual links are off", () => {
    const wrapper = mount(ArtistCredit, { props: { song: duet } });
    const links = wrapper.findAll("a");
    expect(links).toHaveLength(1);
    expect(links[0]!.text()).toBe("Dolly Parton & Kenny Rogers");
    expect(links[0]!.attributes("href")).toBe(
      "/song/artist?name=Dolly%20Parton%20%26%20Kenny%20Rogers",
    );
  });

  it("links each individual artist in place", () => {
    const wrapper = mount(ArtistCredit, { props: { song: duet, individual: true } });
    expect(wrapper.text()).toBe("Dolly Parton & Kenny Rogers");
    expect(wrapper.findAll("a").map((a) => a.attributes("href"))).toEqual([
      "/song/artist?name=Dolly%20Parton",
      "/song/artist?name=Kenny%20Rogers",
    ]);
  });

  it("appends artists that aren't in the credit", () => {
    const wrapper = mount(ArtistCredit, { props: { song: featured, individual: true } });
    expect(wrapper.text().replace(/\s+/g, " ")).toBe("Lindsey Stirling (with ZZ Ward)");
    expect(wrapper.findAll("a").map((a) => a.text())).toEqual(["Lindsey Stirling", "ZZ Ward"]);
  });
});
