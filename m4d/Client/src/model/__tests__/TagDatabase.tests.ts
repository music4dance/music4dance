import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";
import { TagList } from "../TagList";

describe("tag database", () => {
  it("should load", () => {
    const loaded = getEnvironmentMock();

    expect(loaded).toBeDefined();
    expect(loaded?.tagDatabase).toBeDefined();
    expect(loaded?.tagDatabase.tags).toBeDefined();
    expect(loaded!.tagDatabase.tags.length > 0).toBeTruthy();
  });

  it("should get primary", () => {
    const tagDatabase = getEnvironmentMock().tagDatabase;

    const dp = tagDatabase.getPrimary("Dance Pop:Music");
    expect(dp?.key).toEqual("Dance Pop:Music");
    const dp2 = tagDatabase.getPrimary("dance-pop:Music");
    expect(dp2?.key).toEqual("Dance Pop:Music");
    const fb = tagDatabase.getPrimary("foo-bar:Music");
    expect(fb).toBeUndefined();
  });

  it("can normalize", () => {
    const tagDatabase = getEnvironmentMock().tagDatabase;

    const n = tagDatabase.normalizeTagList(
      new TagList("folk-rock:Music:5|Dance Pop:Music:3|2000s:Other:7")
    );
    expect(n.summary).toBeDefined();
    expect(n.summary).toEqual(
      "Folk Rock:Music:5|Dance Pop:Music:3|2000S:Other:7"
    );
  });
});
