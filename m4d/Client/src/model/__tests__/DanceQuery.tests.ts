import "reflect-metadata";
import { DanceQuery } from "../DanceQuery";
import { DanceQueryBase } from "../DanceQueryBase";

describe("dance query", () => {
  it("should load simple list", () => {
    const dq = new DanceQuery("BCH,BOL");
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(DanceQuery);
    expect(dq.isExclusive).toBeFalsy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(2);
    expect(dq.danceList).toContain("BCH");
    expect(dq.danceList).toContain("BOL");
  });

  it("should load exclusive list", () => {
    const dq = new DanceQuery("AND,BCH,BOL");
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(DanceQuery);
    expect(dq.isExclusive).toBeTruthy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(2);
    expect(dq.danceList).toContain("BCH");
    expect(dq.danceList).toContain("BOL");
  });

  it("should load inferred list", () => {
    const dq = new DanceQuery("OOX,BCH,BOL");
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(DanceQuery);
    expect(dq.isExclusive).toBeFalsy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(2);
    expect(dq.danceList).toContain("BCH");
    expect(dq.danceList).toContain("BOL");
  });

  it("should load exclusive list with inferred", () => {
    const dq = new DanceQuery("ADX,BCH,BOL");
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(DanceQuery);
    expect(dq.isExclusive).toBeTruthy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(2);
    expect(dq.danceList).toContain("BCH");
    expect(dq.danceList).toContain("BOL");
  });

  describe("dance query description", () => {
    it("should handle a single dance", () => {
      const dq = new DanceQuery("BCH");
      expect(dq.description).toEqual("Bachata songs");
    });

    it("should handle a single dance with inferred (legacy)", () => {
      const dq = new DanceQuery("OOX,BCH");
      expect(dq.description).toEqual("Bachata songs");
    });

    it("should handle a two dances, inclusive", () => {
      const dq = new DanceQuery("BCH,BOL");
      expect(dq.description).toEqual(
        "songs danceable to any of Bachata or Bolero"
      );
    });

    it("should handle a two dances, exclusive", () => {
      const dq = new DanceQuery("AND,BCH,BOL");
      expect(dq.description).toEqual(
        "songs danceable to all of Bachata and Bolero"
      );
    });

    it("should handle a multiple dances, inclusive", () => {
      const dq = new DanceQuery("BCH,BOL,BLU");
      expect(dq.description).toEqual(
        "songs danceable to any of Bachata, Bolero or Blues"
      );
    });

    it("should handle a multiple dances, exclusive", () => {
      const dq = new DanceQuery("AND,BCH,BOL,BLU");
      expect(dq.description).toEqual(
        "songs danceable to all of Bachata, Bolero and Blues"
      );
    });
  });
});
