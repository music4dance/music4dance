import { describe, it, expect } from "vitest";
import { SongProperty } from "../SongProperty";

describe("SongProperty", () => {
  describe("constructor and basic properties", () => {
    it("should create property with name and value", () => {
      const prop = new SongProperty({ name: "Title", value: "Test Song" });
      expect(prop.name).toBe("Title");
      expect(prop.value).toBe("Test Song");
    });

    it("should parse baseName from simple name", () => {
      const prop = new SongProperty({ name: "Title", value: "Test" });
      expect(prop.baseName).toBe("Title");
    });

    it("should parse baseName from indexed name", () => {
      const prop = new SongProperty({ name: "Album:00", value: "Greatest Hits" });
      expect(prop.baseName).toBe("Album");
    });

    it("should parse baseName from qualified name", () => {
      const prop = new SongProperty({ name: "Purchase:00:S", value: "track123" });
      expect(prop.baseName).toBe("Purchase");
    });

    it("should detect action names", () => {
      const editProp = new SongProperty({ name: ".Edit", value: "" });
      expect(editProp.isAction).toBe(true);

      const normalProp = new SongProperty({ name: "Title", value: "" });
      expect(normalProp.isAction).toBe(false);
    });
  });

  describe("FromString", () => {
    it("should parse property from string with value", () => {
      const prop = SongProperty.FromString("Title=My Song");
      expect(prop.name).toBe("Title");
      expect(prop.value).toBe("My Song");
    });

    it("should parse property from string without value", () => {
      const prop = SongProperty.FromString(".Edit");
      expect(prop.name).toBe(".Edit");
      expect(prop.value).toBeUndefined();
    });

    it("should handle equals sign in value", () => {
      const prop = SongProperty.FromString("Tag+=CHA=International:Style");
      expect(prop.name).toBe("Tag+");
      expect(prop.value).toBe("CHA=International:Style");
    });
  });

  describe("fromParts", () => {
    it("should create simple property", () => {
      const prop = SongProperty.fromParts("Title", "My Song");
      expect(prop.name).toBe("Title");
      expect(prop.value).toBe("My Song");
      expect(prop.toString()).toBe("Title=My Song");
    });

    it("should create indexed property with zero-padded index", () => {
      const prop = SongProperty.fromParts("Album", "Greatest Hits", 0);
      expect(prop.name).toBe("Album:00");
      expect(prop.value).toBe("Greatest Hits");
    });

    it("should create indexed property with double-digit index", () => {
      const prop = SongProperty.fromParts("Album", "Best Of", 15);
      expect(prop.name).toBe("Album:15");
    });

    it("should create qualified property", () => {
      const prop = SongProperty.fromParts("Purchase", "track123", 0, "S");
      expect(prop.name).toBe("Purchase:00:S");
      expect(prop.value).toBe("track123");
    });

    it("should handle undefined value", () => {
      const prop = SongProperty.fromParts("Title");
      expect(prop.name).toBe("Title");
      expect(prop.value).toBe("");
    });

    it("should convert numeric value to string", () => {
      const prop = SongProperty.fromParts("Tempo", 120.5);
      expect(prop.value).toBe("120.5");
    });
  });

  describe("fromAddedTag", () => {
    it("should create Tag+ property for dance tag", () => {
      const prop = SongProperty.fromAddedTag("Cha Cha:Dance");
      expect(prop.name).toBe("Tag+");
      expect(prop.value).toBe("Cha Cha:Dance");
      expect(prop.toString()).toBe("Tag+=Cha Cha:Dance");
    });

    it("should create Tag+ property for style tag", () => {
      const prop = SongProperty.fromAddedTag("CHA=International:Style");
      expect(prop.name).toBe("Tag+");
      expect(prop.value).toBe("CHA=International:Style");
    });

    it("should create Tag+ property for tempo tag", () => {
      const prop = SongProperty.fromAddedTag("Fast:Tempo");
      expect(prop.name).toBe("Tag+");
      expect(prop.value).toBe("Fast:Tempo");
    });
  });

  describe("fromRemovedTag", () => {
    it("should create Tag- property", () => {
      const prop = SongProperty.fromRemovedTag("Waltz:Dance");
      expect(prop.name).toBe("Tag-");
      expect(prop.value).toBe("Waltz:Dance");
      expect(prop.toString()).toBe("Tag-=Waltz:Dance");
    });

    it("should create Tag- property for style tag", () => {
      const prop = SongProperty.fromRemovedTag("WAL=American:Style");
      expect(prop.name).toBe("Tag-");
      expect(prop.value).toBe("WAL=American:Style");
    });
  });

  describe("fromDanceFamilyTags", () => {
    it("should create property with single family", () => {
      const prop = SongProperty.fromDanceFamilyTags("CHA", ["International"]);
      expect(prop.name).toBe("Tag+:CHA");
      expect(prop.value).toBe("International:Style");
      expect(prop.toString()).toBe("Tag+:CHA=International:Style");
    });

    it("should create property with multiple families pipe-separated", () => {
      const prop = SongProperty.fromDanceFamilyTags("CHA", ["American", "International"]);
      expect(prop.name).toBe("Tag+:CHA");
      expect(prop.value).toBe("American:Style|International:Style");
      expect(prop.toString()).toBe("Tag+:CHA=American:Style|International:Style");
    });

    it("should create property with three families", () => {
      const prop = SongProperty.fromDanceFamilyTags("TWS", [
        "International",
        "American",
        "Country",
      ]);
      expect(prop.name).toBe("Tag+:TWS");
      expect(prop.value).toBe("International:Style|American:Style|Country:Style");
    });

    it("should handle empty families array", () => {
      const prop = SongProperty.fromDanceFamilyTags("CHA", []);
      expect(prop.name).toBe("Tag+:CHA");
      expect(prop.value).toBe("");
    });
  });

  describe("fromDanceRating", () => {
    it("should create positive rating property", () => {
      const prop = SongProperty.fromDanceRating("CHA", 1);
      expect(prop.name).toBe("DanceRating");
      expect(prop.value).toBe("CHA+1");
      expect(prop.toString()).toBe("DanceRating=CHA+1");
    });

    it("should create negative rating property", () => {
      const prop = SongProperty.fromDanceRating("WAL", -2);
      expect(prop.name).toBe("DanceRating");
      expect(prop.value).toBe("WAL-2");
      expect(prop.toString()).toBe("DanceRating=WAL-2");
    });

    it("should create zero rating property with plus sign", () => {
      const prop = SongProperty.fromDanceRating("FOX", 0);
      expect(prop.value).toBe("FOX+0");
    });

    it("should create large positive rating", () => {
      const prop = SongProperty.fromDanceRating("CHA", 10);
      expect(prop.value).toBe("CHA+10");
    });

    it("should create large negative rating", () => {
      const prop = SongProperty.fromDanceRating("CHA", -5);
      expect(prop.value).toBe("CHA-5");
    });
  });

  describe("BuildIndexName", () => {
    it("should build name with zero-padded index", () => {
      const name = SongProperty.BuildIndexName("Album", 0);
      expect(name).toBe("Album:00");
    });

    it("should build name with modifier", () => {
      const name = SongProperty.BuildIndexName("Purchase", 0, "S");
      expect(name).toBe("Purchase:00:S");
    });

    it("should handle double-digit index", () => {
      const name = SongProperty.BuildIndexName("Album", 15);
      expect(name).toBe("Album:15");
    });
  });

  describe("index parsing", () => {
    it("should parse index from indexed property", () => {
      const prop = new SongProperty({ name: "Album:00", value: "Test" });
      expect(prop.hasIndex).toBe(true);
      expect(prop.index).toBe(0);
      expect(prop.safeIndex).toBe(0);
    });

    it("should parse index from qualified property", () => {
      const prop = new SongProperty({ name: "Purchase:05:S", value: "test" });
      expect(prop.hasIndex).toBe(true);
      expect(prop.index).toBe(5);
    });

    it("should return undefined for simple property", () => {
      const prop = new SongProperty({ name: "Title", value: "Test" });
      expect(prop.hasIndex).toBe(false);
      expect(prop.safeIndex).toBeUndefined();
    });

    it("should throw error for missing index when unsafe", () => {
      const prop = new SongProperty({ name: "Title", value: "Test" });
      expect(() => prop.index).toThrow();
    });
  });

  describe("qualifier parsing", () => {
    it("should parse qualifier from qualified property", () => {
      const prop = new SongProperty({ name: "Purchase:00:S", value: "test" });
      expect(prop.qualifier).toBe("S");
    });

    it("should return undefined for unqualified property", () => {
      const prop = new SongProperty({ name: "Album:00", value: "test" });
      expect(prop.qualifier).toBeUndefined();
    });

    it("should return undefined for simple property", () => {
      const prop = new SongProperty({ name: "Title", value: "test" });
      expect(prop.qualifier).toBeUndefined();
    });
  });

  describe("danceQualifier parsing", () => {
    it("should parse dance qualifier from Tag property", () => {
      const prop = new SongProperty({ name: "Tag+:CHA", value: "International:Style" });
      expect(prop.danceQualifier).toBe("CHA");
    });

    it("should return undefined for simple Tag property", () => {
      const prop = new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" });
      expect(prop.danceQualifier).toBeUndefined();
    });
  });

  describe("real-world scenarios", () => {
    it("should handle complete Cha Cha upvote with multiple families", () => {
      const rating = SongProperty.fromDanceRating("CHA", 1);
      const danceTag = SongProperty.fromAddedTag("Cha Cha:Dance");
      const familyTags = SongProperty.fromDanceFamilyTags("CHA", ["American", "International"]);

      expect(rating.toString()).toBe("DanceRating=CHA+1");
      expect(danceTag.toString()).toBe("Tag+=Cha Cha:Dance");
      expect(familyTags.toString()).toBe("Tag+:CHA=American:Style|International:Style");
    });

    it("should handle filtered Cha Cha upvote with single family", () => {
      const rating = SongProperty.fromDanceRating("CHA", 1);
      const danceTag = SongProperty.fromAddedTag("Cha Cha:Dance");
      const familyTag = SongProperty.fromDanceFamilyTags("CHA", ["International"]);

      expect(rating.toString()).toBe("DanceRating=CHA+1");
      expect(danceTag.toString()).toBe("Tag+=Cha Cha:Dance");
      expect(familyTag.toString()).toBe("Tag+:CHA=International:Style");
    });

    it("should handle downvote scenario", () => {
      const rating = SongProperty.fromDanceRating("WAL", -1);
      const danceTag = SongProperty.fromRemovedTag("Waltz:Dance");

      expect(rating.toString()).toBe("DanceRating=WAL-1");
      expect(danceTag.toString()).toBe("Tag-=Waltz:Dance");
    });

    it("should handle album with multiple tracks", () => {
      const album0 = SongProperty.fromParts("Album", "Greatest Hits Vol 1", 0);
      const track0 = SongProperty.fromParts("Track", "5", 0);
      const purchase0 = SongProperty.fromParts("Purchase", "single123", 0, "S");

      const album1 = SongProperty.fromParts("Album", "Greatest Hits Vol 2", 1);
      const track1 = SongProperty.fromParts("Track", "3", 1);
      const purchase1 = SongProperty.fromParts("Purchase", "album456", 1, "A");

      expect(album0.name).toBe("Album:00");
      expect(track0.name).toBe("Track:00");
      expect(purchase0.name).toBe("Purchase:00:S");

      expect(album1.name).toBe("Album:01");
      expect(track1.name).toBe("Track:01");
      expect(purchase1.name).toBe("Purchase:01:A");
    });
  });

  describe("toString", () => {
    it("should format property as name=value", () => {
      const prop = new SongProperty({ name: "Title", value: "My Song" });
      expect(prop.toString()).toBe("Title=My Song");
    });

    it("should format property with empty value", () => {
      const prop = new SongProperty({ name: ".Edit", value: "" });
      expect(prop.toString()).toBe(".Edit=");
    });

    it("should format indexed property", () => {
      const prop = SongProperty.fromParts("Album", "Greatest Hits", 0);
      expect(prop.toString()).toBe("Album:00=Greatest Hits");
    });
  });
});
