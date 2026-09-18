import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { BButton, BFormTags } from "bootstrap-vue-next";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import ArtistsEditor from "../ArtistsEditor.vue";
import { Song } from "@/models/Song";
import { SongHistory } from "@/models/SongHistory";
import { SongProperty } from "@/models/SongProperty";

setupTestEnvironment();

function song(extra: string[]): Song {
  const log = [
    ".Create=",
    "User=dwgray",
    "Time=01/15/2024 14:30:00",
    "Title=Islands in the Stream",
    "Artist=Dolly Parton & Kenny Rogers",
    ...extra,
  ];
  const properties = log.map((entry) => {
    const eq = entry.indexOf("=");
    return new SongProperty({ name: entry.slice(0, eq), value: entry.slice(eq + 1) });
  });
  return Song.fromHistory(new SongHistory({ id: "s1", properties }));
}

function mountEditor(s: Song) {
  return mount(ArtistsEditor, {
    props: { song: s },
    global: { components: { BButton, BFormTags } },
  });
}

const botSplit = [
  ".Edit=",
  "User=artist-bot|P",
  "Time=01/16/2024 14:30:00",
  "Artists=Dolly Parton|Kenny Rogers",
];

describe("ArtistsEditor.vue", () => {
  it("shows the source and offers Don't split for a split song", () => {
    const wrapper = mountEditor(song(botSplit));
    expect(wrapper.text()).toContain("(automatic)");
    const buttons = wrapper.findAll("button").map((b) => b.text());
    expect(buttons).toContain("Don't split");
    expect(buttons).not.toContain("Reset to automatic");
  });

  it("emits the whole credit for Don't split", async () => {
    const wrapper = mountEditor(song(botSplit));
    const dontSplit = wrapper.findAll("button").find((b) => b.text() === "Don't split")!;
    await dontSplit.trigger("click");
    expect(wrapper.emitted("update-artists")![0]).toEqual([["Dolly Parton & Kenny Rogers"]]);
  });

  it("offers Reset to automatic for a human-edited list", async () => {
    const wrapper = mountEditor(song(["Artists=Dolly Parton & Kenny Rogers"]));
    expect(wrapper.text()).toContain("(edited)");
    const reset = wrapper.findAll("button").find((b) => b.text() === "Reset to automatic")!;
    await reset.trigger("click");
    expect(wrapper.emitted("update-artists")![0]).toEqual([undefined]);
  });

  it("emits the edited list when tags change", async () => {
    const wrapper = mountEditor(song(botSplit));
    wrapper.findComponent(BFormTags).vm.$emit("update:modelValue", ["Dolly Parton"]);
    expect(wrapper.emitted("update-artists")![0]).toEqual([["Dolly Parton"]]);
  });
});
