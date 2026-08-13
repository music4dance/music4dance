import { describe, expect, it } from "vitest";
import { SongSort } from "../SongSort";

describe("SongSort", () => {
  describe("change", () => {
    it("switches to a new column ascending when no sort is set", () => {
      const sort = new SongSort();

      const changed = sort.change("Title");

      expect(changed.id).toEqual("Title");
      expect(changed.direction).toEqual("asc");
    });

    it("switches to a new column ascending when a different column was sorted ascending", () => {
      const sort = new SongSort("Artist");

      const changed = sort.change("Title");

      expect(changed.id).toEqual("Title");
      expect(changed.direction).toEqual("asc");
    });

    it("switches to a new column ascending when a different column was sorted descending", () => {
      const sort = new SongSort("Tempo_desc");

      const changed = sort.change("Artist");

      expect(changed.id).toEqual("Artist");
      expect(changed.direction).toEqual("asc");
    });

    it("flips the same column from ascending to descending", () => {
      const sort = new SongSort("Title");

      const changed = sort.change("Title");

      expect(changed.id).toEqual("Title");
      expect(changed.direction).toEqual("desc");
      expect(changed.query).toEqual("Title_desc");
    });

    it("flips the same column from descending back to ascending", () => {
      const sort = new SongSort("Title_desc");

      const changed = sort.change("Title");

      expect(changed.id).toEqual("Title");
      expect(changed.direction).toEqual("asc");
      expect(changed.query).toEqual("Title");
    });

    it("is case-insensitive when matching the clicked column against the current one", () => {
      const sort = new SongSort("Title");

      const changed = sort.change("title");

      expect(changed.query).toEqual("Title_desc");
    });
  });
});
