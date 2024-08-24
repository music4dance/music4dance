import { KeywordQuery } from "../KeywordQuery";
import { describe, expect, it } from "vitest";

describe("KeywordQuery", () => {
  it("should correctly detect lucene === true", () => {
    expect(new KeywordQuery("`test").isLucene).toBe(true);
  });

  it("should correctly detect lucene === false", () => {
    expect(new KeywordQuery("test").isLucene).toBe(false);
  });

  it("should return isLucene === false on an empty string", () => {
    expect(new KeywordQuery("").isLucene).toBe(false);
  });

  it("should return isLucene === false on undefined input", () => {
    expect(new KeywordQuery().isLucene).toBe(false);
  });

  it("should return search === test for simple syntax", () => {
    expect(new KeywordQuery("test").search).toBe("test");
  });

  it("should return search === test for lucene syntax", () => {
    expect(new KeywordQuery("`test").search).toBe("test");
  });

  it("description is accurate for simple search", () => {
    expect(new KeywordQuery("test").description).toBe('containing the text "test"');
  });

  it("description is accurate for lucene search without fields", () => {
    expect(new KeywordQuery("`test").description).toBe('containing the text "test"');
  });

  it("description is accurate for a single field", () => {
    expect(new KeywordQuery("`Title:(test)").description).toBe('where title contains "test"');
  });

  it("description is accurate for a multiple fields", () => {
    expect(new KeywordQuery("`Title:(test) Artist:(foo)").description).toBe(
      'where title contains "test" and artist contains "foo"',
    );
  });

  it("description is accurate for a multiple fields + all", () => {
    expect(new KeywordQuery("`Title:(test) Artist:(foo) bar").description).toBe(
      'containing the text "bar" anywhere and title contains "test" and artist contains "foo"',
    );
  });

  describe("update", () => {
    it("should add a part when value is provided", () => {
      const searchQuery = new KeywordQuery("something");
      const updatedQuery = searchQuery.update("Title", "test");
      expect(updatedQuery.query).toBe("`Title:(test) something");
    });

    it("should delete a part when value is not provided", () => {
      const searchQuery = new KeywordQuery("Title:(test) something");
      const updatedQuery = searchQuery.update("Title", "");
      expect(updatedQuery.query).toBe("something");
    });
  });
});
