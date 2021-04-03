import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { TaggableObject } from "../TaggableObject";

const complex = {
  tags: [
    { value: "Dance Pop", category: "Music", count: 5 },
    { value: "Washington Indie", category: "Music", count: 3 },
    { value: "Unconditional", category: "Other", count: 1 },
  ],
  currentUserTags: [
    { value: "Dance Pop", category: "Music", count: 1 },
    { value: "Washington Indie", category: "Music", count: 1 },
  ],
};

describe("taggable object", () => {
  it("should be creatable", () => {
    const t = new TaggableObject();
    expect(t.tags).toBeDefined();
    expect(t.tags.length).toEqual(0);
    expect(t.currentUserTags).toBeDefined();
    expect(t.currentUserTags.length).toEqual(0);
  });

  it("should manage simple add", () => {
    const t = new TaggableObject();
    t.addTags("dance-pop:Music");
    expect(t.tags.length).toEqual(1);
    expect(t.tags[0].key).toEqual("Dance Pop:Music");
  });

  it("should manage simple remove", () => {
    const t = new TaggableObject();
    t.addTags("dance-pop:Music");
    t.removeTags("Dance Pop:Music");
    expect(t.tags.length).toEqual(0);
  });

  it("should manage simple add for user", () => {
    const t = new TaggableObject();
    t.addTags("dance-pop:Music", true);
    expect(t.tags.length).toEqual(1);
    expect(t.tags[0].key).toEqual("Dance Pop:Music");
    expect(t.currentUserTags.length).toEqual(1);
    expect(t.currentUserTags[0].key).toEqual("Dance Pop:Music");
  });

  it("should manage complex tag manipulation", () => {
    const t = TypedJSON.parse(complex, TaggableObject);
    expect(t).toBeDefined();
    expect(t?.tags.length).toEqual(3);
    expect(t?.currentUserTags.length).toEqual(2);

    t?.addTags("40'S:Other", true);
    expect(t?.tags.length).toEqual(4);
    expect(t?.currentUserTags.length).toEqual(3);

    t?.addTags("Washington Indie:Music");
    expect(t?.tags.length).toEqual(4);
    expect(t?.currentUserTags.length).toEqual(3);

    t?.removeTags("Unconditional:Other");
    expect(t?.tags.length).toEqual(3);
    expect(t?.currentUserTags.length).toEqual(3);

    t?.removeTags("dance-pop:Music");
    expect(t?.tags.length).toEqual(3);
    expect(t?.currentUserTags.length).toEqual(3);
    const dpm = t?.tags.find((x) => x.key === "Dance Pop:Music");
    expect(dpm).toBeDefined();
    expect(dpm?.count).toEqual(4);

    t?.removeTags("Washington Indie:Music", true);
    expect(t?.tags.length).toEqual(3);
    expect(t?.currentUserTags.length).toEqual(2);
    const wim = t?.tags.find((x) => x.key === "Washington Indie:Music");
    expect(wim).toBeDefined();
    expect(wim?.count).toEqual(3);
  });
});
