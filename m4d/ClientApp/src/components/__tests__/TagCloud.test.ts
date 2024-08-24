import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import { modalManagerPlugin } from "bootstrap-vue-next";
import { loadTagsFromString } from "@/helpers/TagLoader";
import TagCloud from "../TagCloud.vue";

const tagsJson = [
  {
    key: "1940S:Other",
    count: 215,
  },
  {
    key: "1949:Other",
    count: 2,
  },
  {
    key: "Jazz:Music",
    count: 132,
  },
  {
    key: "Rock:Music",
    count: 7,
  },
  {
    key: "Changing:Tempo",
    count: 2,
  },
  {
    key: "Contemporary:Style",
    count: 323,
  },
];

const tagDatabase = loadTagsFromString(JSON.stringify(tagsJson));

describe("TagCloud.vue", () => {
  test("checks raw tags", () => {
    const tags = tagDatabase.tags;
    expect(tags.length).toBe(6);
    const tag = tagDatabase.getTag("1940s:Other");
    expect(tag).toBeDefined();
    expect(tag!.key).toBe("1940S:Other");
  });

  test("Renders a simple tag cloud", () => {
    const tags = tagDatabase.tags;
    const wrapper = mount(TagCloud, {
      props: { tags },
      global: { plugins: [modalManagerPlugin] },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });

  test("Renders a tag cloud without filters", () => {
    const tags = tagDatabase.tags;
    const wrapper = mount(TagCloud, {
      props: { tags, hideFilter: true },
      global: { plugins: [modalManagerPlugin] },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });

  test("Check tag cloud filtering", async () => {
    const tags = tagDatabase.tags;
    const wrapper = mount(TagCloud, {
      props: { tags },
      global: { plugins: [modalManagerPlugin] },
    });

    // Rock is filtered out by strictness
    expect(wrapper.findAll(".text-bg-music").length).toBe(1);

    // Set strictness to include all tags
    const strictness = wrapper.find("#strictness");
    await strictness.setValue(10);

    // Now both music tags are shown
    expect(wrapper.findAll(".text-bg-music").length).toBe(2);

    // Filter out music tags
    const music = wrapper.find("#filter-music");
    expect(music).toBeDefined();
    await music.trigger("click");

    // No music tags are shown
    expect(wrapper.findAll(".text-bg-music").length).toBe(0);

    // But both other tags are shown
    expect(wrapper.findAll(".text-bg-other").length).toBe(2);
  });
});
