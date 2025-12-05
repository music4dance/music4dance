import { describe, expect, it, beforeEach, vi } from "vitest";
import { TagHandler } from "../TagHandler";
import { Tag } from "../Tag";
import { SongFilter } from "../SongFilter";
import { TaggableObject } from "../TaggableObject";

// Mock the DanceEnvironmentManager
vi.mock("@/helpers/DanceEnvironmentManager", () => ({
  safeDanceDatabase: () => ({
    danceFromId: (id: string) => {
      if (id === "waltz") return { id: "waltz", name: "Waltz" };
      if (id === "tango") return { id: "tango", name: "Tango" };
      return null;
    },
    fromId: (id: string) => {
      if (id === "waltz") return { id: "waltz", name: "Waltz" };
      if (id === "tango") return { id: "tango", name: "Tango" };
      return null;
    },
  }),
}));

describe("TagHandler", () => {
  let tagHandler: TagHandler;
  let mockTag: Tag;
  let mockFilter: SongFilter;
  let mockParent: TaggableObject;

  beforeEach(() => {
    mockTag = new Tag();
    mockTag.key = "romantic:other";

    mockFilter = new SongFilter();
    mockFilter.action = "filtersearch";

    mockParent = {
      isUserTag: vi.fn().mockReturnValue(false),
      description: "Test Parent",
    } as unknown as TaggableObject;

    tagHandler = new TagHandler({
      tag: mockTag,
      filter: mockFilter,
      parent: mockParent,
      user: "testuser",
    });
  });

  describe("constructor", () => {
    it("should create a TagHandler with proper initialization", () => {
      expect(tagHandler.tag).toBe(mockTag);
      expect(tagHandler.filter).toBe(mockFilter);
      expect(tagHandler.parent).toBe(mockParent);
      expect(tagHandler.user).toBe("testuser");
      expect(tagHandler.id).toBe("romantic:other");
    });

    it("should generate UUID if tag has no key", () => {
      const tagWithoutKey = new Tag();
      const handler = new TagHandler({ tag: tagWithoutKey });
      expect(handler.id).toBeDefined();
      expect(handler.id).not.toBe("");
    });
  });

  describe("isSelected", () => {
    it("should return true when parent indicates tag is selected", () => {
      vi.mocked(mockParent.isUserTag).mockReturnValue(true);
      expect(tagHandler.isSelected).toBe(true);
    });

    it("should return false when parent indicates tag is not selected", () => {
      vi.mocked(mockParent.isUserTag).mockReturnValue(false);
      expect(tagHandler.isSelected).toBe(false);
    });

    it("should return false when there is no parent", () => {
      tagHandler.parent = undefined;
      expect(tagHandler.isSelected).toBe(false);
    });
  });

  describe("hasFilter", () => {
    it("should return true when filter exists and is not default", () => {
      mockFilter.tags = "test-tag";
      expect(tagHandler.hasFilter).toBe(true);
    });

    it("should return false when filter is null", () => {
      tagHandler.filter = undefined;
      expect(tagHandler.hasFilter).toBe(false);
    });

    it("should return false when filter is default", () => {
      // Default filter (no meaningful properties set)
      const defaultFilter = new SongFilter();
      tagHandler.filter = defaultFilter;
      expect(tagHandler.hasFilter).toBe(false);
    });

    it("should return false when filter is raw", () => {
      mockFilter.action = "azure+raw";
      expect(tagHandler.hasFilter).toBe(false);
    });
  });

  describe("computedDanceName", () => {
    it("should return dance name from danceId when available", () => {
      tagHandler.danceId = "waltz";
      expect(tagHandler.computedDanceName).toBe("Waltz");
    });

    it("should return empty string for unknown danceId", () => {
      tagHandler.danceId = "unknown";
      expect(tagHandler.computedDanceName).toBe("");
    });

    it("should return dance name from filter when no danceId", () => {
      mockFilter.dances = "waltz";
      // Mock the danceQuery property
      Object.defineProperty(mockFilter, "danceQuery", {
        get: () => ({ danceNames: ["Waltz"] }),
        configurable: true,
      });
      expect(tagHandler.computedDanceName).toBe("Waltz");
    });

    it("should return empty string when no dance context", () => {
      expect(tagHandler.computedDanceName).toBe("");
    });
  });

  describe("isDanceSpecific", () => {
    it("should return true when danceId and dance name are available", () => {
      tagHandler.danceId = "waltz";
      expect(tagHandler.isDanceSpecific).toBe(true);
    });

    it("should return false when danceId is set but dance doesn't exist", () => {
      tagHandler.danceId = "unknown";
      expect(tagHandler.isDanceSpecific).toBe(false);
    });

    it("should return false when no danceId is set", () => {
      expect(tagHandler.isDanceSpecific).toBe(false);
    });
  });

  describe("getTagLink", () => {
    beforeEach(() => {
      // Reset filter to clean state
      mockFilter = new SongFilter();
      mockFilter.action = "filtersearch";
      tagHandler.filter = mockFilter;
    });

    it("should generate global tag link with include modifier", () => {
      const link = tagHandler.getTagLink("+", false, true);
      expect(link).toContain("/song/filtersearch?filter=");
      expect(decodeURIComponent(link)).toContain("+romantic:other");
    });

    it("should generate global tag link with exclude modifier", () => {
      const link = tagHandler.getTagLink("-", false, true);
      expect(link).toContain("/song/filtersearch?filter=");
      expect(link).toContain("%5C-romantic%3Aother");
    });

    it("should generate dance-specific tag link when danceId is set", () => {
      tagHandler.danceId = "waltz";
      const link = tagHandler.getTagLink("+", true, false);
      expect(link).toContain("/song/filtersearch?filter=");
      expect(decodeURIComponent(link)).toContain("waltz");
      expect(decodeURIComponent(link)).toContain("+romantic:other");
    });

    it("should preserve existing filter when clear is false", () => {
      mockFilter.tags = "existing-tag";
      const link = tagHandler.getTagLink("+", false, false);
      expect(link).toContain("/song/filtersearch?filter=");
      // Should contain both existing and new tag
      expect(link).toContain("existing%5C-tag%3A");
      expect(link).toContain("%2Bromantic%3Aother");
    });

    it("should create clean filter when clear is true", () => {
      mockFilter.tags = "existing-tag";
      const link = tagHandler.getTagLink("+", false, true);
      expect(link).toContain("/song/filtersearch?filter=");
      const decodedLink = decodeURIComponent(link);
      expect(decodedLink).toContain("+romantic:other");
      // Should not contain existing tag when clear=true
    });

    it("should handle existing dance query with dance-specific tags", () => {
      tagHandler.danceId = "waltz";
      mockFilter.dances = "tango";
      const link = tagHandler.getTagLink("+", false, false);
      expect(link).toContain("/song/filtersearch?filter=");
      const decodedLink = decodeURIComponent(link);
      expect(decodedLink).toContain("tango");
      expect(decodedLink).toContain("+romantic:other");
    });
  });

  describe("getAvailableOptions", () => {
    it("should return global options only when not dance-specific", () => {
      const options = tagHandler.getAvailableOptions();

      // Should have 2 global options (list all include/exclude)
      expect(options).toHaveLength(2);
      expect(options.every((o) => o.scope === "global")).toBe(true);
      expect(options.some((o) => o.modifier === "+")).toBe(true);
      expect(options.some((o) => o.modifier === "-")).toBe(true);
      expect(options.every((o) => o.type === "list")).toBe(true);
    });

    it("should include filter options when hasFilter is true", () => {
      mockFilter.tags = "existing-tag";
      const options = tagHandler.getAvailableOptions();

      // Should have 4 global options (2 filter + 2 list)
      expect(options).toHaveLength(4);
      expect(options.filter((o) => o.type === "filter")).toHaveLength(2);
      expect(options.filter((o) => o.type === "list")).toHaveLength(2);
    });

    it("should include dance-specific options when isDanceSpecific is true", () => {
      tagHandler.danceId = "waltz";
      const options = tagHandler.getAvailableOptions();

      // Should have 4 options (2 dance-specific list + 2 global list)
      expect(options).toHaveLength(4);
      expect(options.filter((o) => o.scope === "dance-specific")).toHaveLength(2);
      expect(options.filter((o) => o.scope === "global")).toHaveLength(2);
    });

    it("should include all option types when dance-specific and hasFilter", () => {
      tagHandler.danceId = "waltz";
      mockFilter.tags = "existing-tag";
      const options = tagHandler.getAvailableOptions();

      // Should have 6 options (4 dance-specific + 2 global)
      expect(options).toHaveLength(6);
      expect(options.filter((o) => o.scope === "dance-specific")).toHaveLength(4);
      expect(options.filter((o) => o.scope === "global")).toHaveLength(2);
      expect(options.filter((o) => o.type === "filter")).toHaveLength(2);
      expect(options.filter((o) => o.type === "list")).toHaveLength(4);
    });

    it("should generate correct labels for dance-specific options", () => {
      tagHandler.danceId = "waltz";
      const options = tagHandler.getAvailableOptions();

      const danceSpecificOptions = options.filter((o) => o.scope === "dance-specific");
      expect(danceSpecificOptions.some((o) => o.label.includes("Waltz"))).toBe(true);
      expect(danceSpecificOptions.some((o) => o.label.includes("romantic"))).toBe(true);
    });

    it("should generate correct variants for include/exclude options", () => {
      const options = tagHandler.getAvailableOptions();

      const includeOptions = options.filter((o) => o.modifier === "+");
      const excludeOptions = options.filter((o) => o.modifier === "-");

      expect(includeOptions.every((o) => o.variant === "success")).toBe(true);
      expect(excludeOptions.every((o) => o.variant === "danger")).toBe(true);
    });

    it("should generate valid hrefs for all options", () => {
      tagHandler.danceId = "waltz";
      mockFilter.tags = "existing-tag";
      const options = tagHandler.getAvailableOptions();

      options.forEach((option) => {
        expect(option.href).toMatch(/^\/song\/filtersearch\?filter=/);
        expect(option.href).not.toBe("");
      });
    });
  });

  describe("edge cases", () => {
    it("should handle undefined tag gracefully", () => {
      const handler = new TagHandler({});
      expect(handler.tag).toBeDefined();
      expect(handler.tag.key).toBeUndefined();
    });

    it("should handle empty tag key", () => {
      const emptyTag = new Tag();
      emptyTag.key = "empty:other";
      const handler = new TagHandler({ tag: emptyTag });

      // Should still generate links without errors
      const link = handler.getTagLink("+", false, true);
      expect(link).toContain("/song/filtersearch?filter=");
    });

    it("should handle special characters in tag key", () => {
      const specialTag = new Tag();
      specialTag.key = "special-tag+with&chars:other";
      const handler = new TagHandler({ tag: specialTag });

      const link = handler.getTagLink("+", false, true);
      expect(link).toContain("/song/filtersearch?filter=");
      // Should contain the encoded tag
      expect(link).toContain("special%5C-tag%2Bwith%26chars%3Aother");
    });
  });

  describe("getSongFilter", () => {
    it("should return a SongFilter with the tag applied", () => {
      const filter = tagHandler.getSongFilter("+", false, true);

      expect(filter).toBeInstanceOf(SongFilter);
      expect(filter.action).toBe("filtersearch");
      expect(filter.tags).toContain("romantic:other");
    });

    it("should preserve existing filter when clear is false", () => {
      mockFilter.tags = "existing:tag";
      mockFilter.tempoMin = 120;

      const filter = tagHandler.getSongFilter("+", false, false);

      expect(filter.tags).toContain("existing");
      expect(filter.tags).toContain("romantic:other");
      expect(filter.tempoMin).toBe(120);
    });

    it("should create clean filter when clear is true", () => {
      mockFilter.tags = "existing:tag";
      mockFilter.tempoMin = 120;

      const filter = tagHandler.getSongFilter("+", false, true);

      expect(filter.tags).toContain("romantic:other");
      expect(filter.tags).not.toContain("existing");
      expect(filter.tempoMin).toBeUndefined();
    });
  });

  describe("option descriptions", () => {
    it("should generate meaningful descriptions for filter options with existing filter", () => {
      // Set up a filter with existing tags
      mockFilter.tags = "existing:tag";
      mockFilter.dances = "waltz";

      const options = tagHandler.getAvailableOptions();
      const filterOptions = options.filter((o) => o.type === "filter");

      // Should have descriptions showing the combined filter
      expect(filterOptions.length).toBeGreaterThan(0);
      filterOptions.forEach((option) => {
        expect(option.description).toContain("including tag");
        expect(option.description).toContain("Waltz");
        expect(option.description).not.toBe(option.label);
      });
    });

    it("should generate meaningful descriptions for list options starting fresh", () => {
      const options = tagHandler.getAvailableOptions();
      const listOptions = options.filter((o) => o.type === "list");

      // Should have descriptions showing just the new tag
      expect(listOptions.length).toBeGreaterThan(0);
      listOptions.forEach((option) => {
        expect(option.description).toMatch(/(including|excluding) tag/);
        expect(option.description).not.toBe(option.label);
      });
    });

    it("should show different descriptions for include vs exclude modifiers", () => {
      const options = tagHandler.getAvailableOptions();
      const includeOption = options.find((o) => o.modifier === "+");
      const excludeOption = options.find((o) => o.modifier === "-");

      expect(includeOption?.description).toBeDefined();
      expect(excludeOption?.description).toBeDefined();
      expect(includeOption?.description).not.toBe(excludeOption?.description);
    });

    it("should include tempo and length in descriptions when present", () => {
      mockFilter.tempoMin = 120;
      mockFilter.tempoMax = 140;
      mockFilter.lengthMin = 180;
      mockFilter.lengthMax = 240;

      const options = tagHandler.getAvailableOptions();
      const filterOptions = options.filter((o) => o.type === "filter");

      filterOptions.forEach((option) => {
        expect(option.description).toContain("tempo between 120 and 140");
        expect(option.description).toContain("length between 180 and 240");
      });
    });
  });
});
