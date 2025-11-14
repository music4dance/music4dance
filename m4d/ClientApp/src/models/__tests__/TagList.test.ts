import { describe, it, expect } from "vitest";
import { Tag } from "../Tag";
import { TagList } from "../TagList";

describe("TagList", () => {
  describe("constructor and tags getter", () => {
    it("should parse empty string to empty array", () => {
      const tagList = new TagList("");
      expect(tagList.tags).toEqual([]);
    });

    it("should parse undefined to empty array", () => {
      const tagList = new TagList(undefined);
      expect(tagList.tags).toEqual([]);
    });

    it("should parse single tag", () => {
      const tagList = new TagList("American:Style");
      expect(tagList.tags).toHaveLength(1);
      expect(tagList.tags[0]?.value).toBe("American");
      expect(tagList.tags[0]?.category).toBe("Style");
    });

    it("should parse multiple tags separated by pipe", () => {
      const tagList = new TagList("American:Style|International:Style");
      expect(tagList.tags).toHaveLength(2);
      expect(tagList.tags[0]?.value).toBe("American");
      expect(tagList.tags[1]?.value).toBe("International");
    });

    it("should ignore ^ prefix for tag parsing", () => {
      const tagList = new TagList("^American:Style|International:Style");
      expect(tagList.tags).toHaveLength(2);
      expect(tagList.tags[0]?.value).toBe("American");
      expect(tagList.tags[1]?.value).toBe("International");
    });
  });

  describe("Adds getter", () => {
    it("should return all tags when not qualified", () => {
      const tagList = new TagList("American:Style|International:Style");
      const adds = tagList.Adds;
      expect(adds).toHaveLength(2);
      expect(adds[0]?.value).toBe("American");
      expect(adds[1]?.value).toBe("International");
    });

    it("should return only + tags and strip prefix when qualified", () => {
      const tagList = new TagList("+American:Style|-International:Style");
      const adds = tagList.Adds;
      expect(adds).toHaveLength(1);
      expect(adds[0]?.value).toBe("American");
      expect(adds[0]?.category).toBe("Style");
    });

    it("should strip + prefix from qualified tags", () => {
      const tagList = new TagList("+American:Style|+International:Style");
      const adds = tagList.Adds;
      expect(adds).toHaveLength(2);
      expect(adds[0]?.value).toBe("American"); // No + prefix
      expect(adds[1]?.value).toBe("International"); // No + prefix
    });

    it("should return empty array when all tags are removes", () => {
      const tagList = new TagList("-American:Style|-International:Style");
      const adds = tagList.Adds;
      expect(adds).toHaveLength(0);
    });
  });

  describe("Removes getter", () => {
    it("should return empty array when not qualified", () => {
      const tagList = new TagList("American:Style|International:Style");
      const removes = tagList.Removes;
      expect(removes).toHaveLength(0);
    });

    it("should return only - tags and strip prefix when qualified", () => {
      const tagList = new TagList("+American:Style|-International:Style");
      const removes = tagList.Removes;
      expect(removes).toHaveLength(1);
      expect(removes[0]?.value).toBe("International");
      expect(removes[0]?.category).toBe("Style");
    });

    it("should strip - prefix from qualified tags", () => {
      const tagList = new TagList("-American:Style|-International:Style");
      const removes = tagList.Removes;
      expect(removes).toHaveLength(2);
      expect(removes[0]?.value).toBe("American"); // No - prefix
      expect(removes[1]?.value).toBe("International"); // No - prefix
    });

    it("should return empty array when all tags are adds", () => {
      const tagList = new TagList("+American:Style|+International:Style");
      const removes = tagList.Removes;
      expect(removes).toHaveLength(0);
    });
  });

  describe("getByCategory", () => {
    it("should return empty array when no matching category", () => {
      const tagList = new TagList("American:Style|International:Style");
      const tempoTags = tagList.getByCategory("Tempo");
      expect(tempoTags).toHaveLength(0);
    });

    it("should filter tags by category", () => {
      const tagList = new TagList("American:Style|Fast:Tempo|International:Style");
      const styleTags = tagList.getByCategory("Style");
      expect(styleTags).toHaveLength(2);
      expect(styleTags[0]?.value).toBe("American");
      expect(styleTags[1]?.value).toBe("International");
    });

    it("should be case-insensitive", () => {
      const tagList = new TagList("American:Style|Fast:Tempo");
      const styleTags = tagList.getByCategory("style");
      expect(styleTags).toHaveLength(1);
      expect(styleTags[0]?.value).toBe("American");
    });

    it("should work with qualified tags", () => {
      const tagList = new TagList("+American:Style|-International:Style|+Fast:Tempo");
      const styleTags = tagList.getByCategory("Style");
      expect(styleTags).toHaveLength(2);
      expect(styleTags[0]?.value).toBe("+American"); // Qualifiers preserved
      expect(styleTags[1]?.value).toBe("-International");
    });

    it("should return empty array for empty tagList", () => {
      const tagList = new TagList("");
      const styleTags = tagList.getByCategory("Style");
      expect(styleTags).toHaveLength(0);
    });
  });

  describe("filterCategories", () => {
    it("should remove specified categories", () => {
      const tagList = new TagList("American:Style|Fast:Tempo|Rock:Music");
      const filtered = tagList.filterCategories(["Style", "Tempo"]);
      expect(filtered.tags).toHaveLength(1);
      expect(filtered.tags[0]?.category).toBe("Music");
    });

    it("should be case-insensitive", () => {
      const tagList = new TagList("American:Style|Fast:Tempo");
      const filtered = tagList.filterCategories(["style"]);
      expect(filtered.tags).toHaveLength(1);
      expect(filtered.tags[0]?.category).toBe("Tempo");
    });

    it("should handle empty category list", () => {
      const tagList = new TagList("American:Style|Fast:Tempo");
      const filtered = tagList.filterCategories([]);
      expect(filtered.tags).toHaveLength(2);
    });
  });

  describe("find", () => {
    it("should find existing tag", () => {
      const tagList = new TagList("American:Style|International:Style");
      const tag = Tag.fromParts("American", "Style");
      const found = tagList.find(tag);
      expect(found).toBeDefined();
      expect(found?.value).toBe("American");
    });

    it("should return undefined for non-existing tag", () => {
      const tagList = new TagList("American:Style");
      const tag = Tag.fromParts("International", "Style");
      const found = tagList.find(tag);
      expect(found).toBeUndefined();
    });

    it("should be case-insensitive", () => {
      const tagList = new TagList("American:Style");
      const tag = Tag.fromParts("american", "style");
      const found = tagList.find(tag);
      expect(found).toBeDefined();
      expect(found?.value).toBe("American");
    });
  });

  describe("add", () => {
    it("should add new tag", () => {
      const tagList = new TagList("American:Style");
      const tag = Tag.fromParts("International", "Style");
      const updated = tagList.add(tag);
      expect(updated.tags).toHaveLength(2);
      expect(updated.tags.map((t) => t.value)).toContain("International");
    });

    it("should replace existing tag", () => {
      const tagList = new TagList("American:Style:5");
      const tag = Tag.fromParts("American", "Style", 10);
      const updated = tagList.add(tag);
      expect(updated.tags).toHaveLength(1);
      expect(updated.tags[0]?.count).toBe(10);
    });

    it("should handle empty tagList", () => {
      const tagList = new TagList("");
      const tag = Tag.fromParts("American", "Style");
      const updated = tagList.add(tag);
      expect(updated.tags).toHaveLength(1);
      expect(updated.tags[0]?.value).toBe("American");
    });
  });

  describe("remove", () => {
    it("should remove existing tag", () => {
      const tagList = new TagList("American:Style|International:Style");
      const tag = Tag.fromParts("American", "Style");
      const updated = tagList.remove(tag);
      expect(updated.tags).toHaveLength(1);
      expect(updated.tags[0]?.value).toBe("International");
    });

    it("should be case-insensitive", () => {
      const tagList = new TagList("American:Style|International:Style");
      const tag = Tag.fromParts("american", "style");
      const updated = tagList.remove(tag);
      expect(updated.tags).toHaveLength(1);
      expect(updated.tags[0]?.value).toBe("International");
    });

    it("should handle non-existing tag", () => {
      const tagList = new TagList("American:Style");
      const tag = Tag.fromParts("International", "Style");
      const updated = tagList.remove(tag);
      expect(updated.tags).toHaveLength(1);
      expect(updated.tags[0]?.value).toBe("American");
    });
  });

  describe("build", () => {
    it("should build tagList from array of tags", () => {
      const tags = [Tag.fromParts("American", "Style"), Tag.fromParts("International", "Style")];
      const tagList = TagList.build(tags);
      expect(tagList.summary).toBe("American:Style|International:Style");
    });

    it("should handle empty array", () => {
      const tagList = TagList.build([]);
      expect(tagList.summary).toBe("");
    });

    it("should include counts", () => {
      const tags = [Tag.fromParts("American", "Style", 5)];
      const tagList = TagList.build(tags);
      expect(tagList.summary).toBe("American:Style:5");
    });
  });

  describe("concat", () => {
    it("should combine two arrays of tags", () => {
      const a = [Tag.fromParts("American", "Style", 5)];
      const b = [Tag.fromParts("International", "Style", 3)];
      const result = TagList.concat(a, b);
      expect(result).toHaveLength(2);
      expect(result[0]?.value).toBe("American");
      expect(result[1]?.value).toBe("International");
    });

    it("should sum counts for duplicate tags", () => {
      const a = [Tag.fromParts("American", "Style", 5)];
      const b = [Tag.fromParts("American", "Style", 3)];
      const result = TagList.concat(a, b);
      expect(result).toHaveLength(1);
      expect(result[0]?.count).toBe(8);
    });

    it("should handle empty arrays", () => {
      const a: Tag[] = [];
      const b = [Tag.fromParts("American", "Style", 5)];
      const result = TagList.concat(a, b);
      expect(result).toHaveLength(1);
      expect(result[0]?.value).toBe("American");
    });

    it("should sort results alphabetically", () => {
      const a = [Tag.fromParts("International", "Style")];
      const b = [Tag.fromParts("American", "Style")];
      const result = TagList.concat(a, b);
      expect(result[0]?.value).toBe("American");
      expect(result[1]?.value).toBe("International");
    });
  });

  describe("AddsDescription and RemovesDescription", () => {
    it("should format Adds description", () => {
      const tagList = new TagList("+American:Style|+International:Style");
      expect(tagList.AddsDescription).toBe("including tags American and International");
    });

    it("should format Removes description", () => {
      const tagList = new TagList("-American:Style|-International:Style");
      expect(tagList.RemovesDescription).toBe("excluding tags American or International");
    });

    it("should handle more than 2 tags", () => {
      const tagList = new TagList("+American:Style|+International:Style|+Country:Style");
      expect(tagList.AddsDescription).toBe("including tags American, International and Country");
    });
  });

  describe("voteFromTags", () => {
    it("should return true if tag is in Adds", () => {
      const tagList = new TagList("American:Style");
      const tag = Tag.fromParts("American", "Style");
      expect(tagList.voteFromTags(tag)).toBe(true);
    });

    it("should return false if negated tag is found", () => {
      const tagList = new TagList("!American:Style");
      const tag = Tag.fromParts("American", "Style");
      expect(tagList.voteFromTags(tag)).toBe(false);
    });

    it("should return undefined if tag not found", () => {
      const tagList = new TagList("International:Style");
      const tag = Tag.fromParts("American", "Style");
      expect(tagList.voteFromTags(tag)).toBeUndefined();
    });
  });

  describe("Integration: Adds with getByCategory", () => {
    it("should extract unqualified Style tags from qualified list", () => {
      // This is the use case in SongFilter.familyTag
      const tagList = new TagList("+American:Style|+Fast:Tempo");
      const styleTags = tagList.Adds.filter((tag) => tag.category === "Style");
      expect(styleTags).toHaveLength(1);
      expect(styleTags[0]?.value).toBe("American"); // No + prefix
      expect(styleTags[0]?.category).toBe("Style");
    });

    it("should work with multiple Style tags", () => {
      const tagList = new TagList("+American:Style|+International:Style|-Country:Style");
      const styleTags = tagList.Adds.filter((tag) => tag.category === "Style");
      expect(styleTags).toHaveLength(2);
      expect(styleTags[0]?.value).toBe("American");
      expect(styleTags[1]?.value).toBe("International");
      expect(styleTags.every((t) => !t.value.startsWith("+"))).toBe(true);
    });

    it("should return empty when only Removes exist", () => {
      const tagList = new TagList("-American:Style|-International:Style");
      const styleTags = tagList.Adds.filter((tag) => tag.category === "Style");
      expect(styleTags).toHaveLength(0);
    });
  });
});
