import { Tag } from "@/model/Tag";
import "reflect-metadata";
import { TagList } from "../TagList";

const qualified =
  "+Bolero:Dance|+Latin:Music|+Nontraditional:Tempo|-Rumba:Dance|-Pop:Music";
const votes = "Bolero:Dance|!Rumba:Dance";

describe("tags loading", () => {
  it("should load taglist", () => {
    const tagList = new TagList(qualified);
    expect(tagList).toBeDefined();
    expect(tagList).toBeInstanceOf(TagList);
    expect(tagList.summary).toEqual(qualified);

    const tags = tagList.tags;
    expect(tags).toBeDefined();
    expect(tags.length).toEqual(5);
    expect(tags[0]).toBeInstanceOf(Tag);
  });

  it("should extract adds", () => {
    const tagList = new TagList(qualified);
    const adds = tagList.Adds;

    expect(adds).toBeDefined();
    expect(adds.length).toEqual(3);
    expect(adds[0]).toBeInstanceOf(Tag);
  });

  it("should extract removes", () => {
    const tagList = new TagList(qualified);
    const removes = tagList.Removes;

    expect(removes).toBeDefined();
    expect(removes.length).toEqual(2);
    expect(removes[0]).toBeInstanceOf(Tag);
  });

  it("should describe adds and removes", () => {
    const tagList = new TagList(qualified);

    expect(tagList.AddsDescription).toEqual(
      "including tags Bolero, Latin and Nontraditional"
    );
    expect(tagList.RemovesDescription).toEqual("excluding tags Rumba or Pop");
  });

  it("should describe filtered adds and removes", () => {
    const tagList = new TagList(qualified).filterCategories(["dance"]);

    expect(tagList.AddsDescription).toEqual(
      "including tags Latin and Nontraditional"
    );
    expect(tagList.RemovesDescription).toEqual("excluding tag Pop");
  });

  it("should handle voting", () => {
    const tagList = new TagList(votes);

    const voteFor = tagList.voteFromTags(
      new Tag({ value: "Bolero", category: "Dance" })
    );
    expect(voteFor).toEqual(true);
    const voteAgainst = tagList.voteFromTags(
      new Tag({ value: "Rumba", category: "Dance" })
    );
    expect(voteAgainst).toEqual(false);
    const noVote = tagList.voteFromTags(
      new Tag({ value: "Swing", category: "Dance" })
    );
    expect(noVote).toBeUndefined();
  });

  it("should handle concat", () => {
    const a = new TagList("A:X:1|B:X:2|F:X:3");
    const b = new TagList("A:X:3|C:X:2|D:X:8|F:X:3");

    const c = TagList.concat(a.tags, b.tags);
    expect(c.length).toEqual(5);

    const r = TagList.build(c).summary;
    expect(r).toEqual("A:X:4|B:X:2|C:X:2|D:X:8|F:X:6");
  });
});
