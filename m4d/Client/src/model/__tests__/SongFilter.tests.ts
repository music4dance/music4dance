import "reflect-metadata";
import { getEnvironmentMock } from "@/helpers/MockEnvironmentManager";
import { SongFilter } from "../SongFilter";

/* eslint-disable-next-line @typescript-eslint/no-explicit-any */
declare const global: any;

beforeAll(() => {
  global.environment = getEnvironmentMock();
});

const simple = "Advanced--Modified";
const basic = "Advanced--Modified---\\-me|h";
const complex =
  "Advanced-ATN,BBA-Dances--A-\\-me|h-100-150--+Big Band:Music|\\-Swing:Music-3";

describe("song filter", () => {
  it("should load basic from string", () => {
    const f = SongFilter.buildFilter(basic);

    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.sortOrder).toEqual("Modified");
    expect(f.user).toEqual("-me|h");
    expect(f.purchase).toBeUndefined();
  });

  it("should load complicated from string", () => {
    const f = SongFilter.buildFilter(complex);

    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.dances).toEqual("ATN,BBA");
    expect(f.sortOrder).toEqual("Dances");
    expect(f.purchase).toEqual("A");
    expect(f.user).toEqual("-me|h");
  });

  it("should pass isEmpty on a trivial filter", () => {
    const f = SongFilter.buildFilter("");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeTruthy();
  });

  it("should pass isEmpty on a simple filter", () => {
    const f = SongFilter.buildFilter(simple);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeTruthy();
  });

  it("should fail isEmpty on a basic filter", () => {
    const f = SongFilter.buildFilter(basic);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
  });

  it("should describe a dance/user filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All Argentine Tango songs excluding songs disliked by me."
    );
  });

  it("should describe a dance/user filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BOL----\\-me|h");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Bolero excluding songs disliked by me."
    );
  });

  it("should describe a dance/tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-----100-150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo between 100 and 150 beats per minute."
    );
  });

  it("should describe a dance/min-tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances_desc----100---");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo greater than 100 beats per minute sorted by Dance Rating from least popular to most popular."
    );
  });

  it("should describe a dance/max-tempo filter", () => {
    const f = SongFilter.buildFilter("Advanced-ATN,BBA-Dances-----150--");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa having" +
        " tempo less than 150 beats per minute sorted by Dance Rating from most popular to least popular."
    );
  });

  it("should describe a full purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----AIS-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs available on Amazon or ITunes or Spotify."
    );
  });

  it("should describe a simple purchase filter", () => {
    const f = SongFilter.buildFilter("Advanced----S-");
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual("All songs available on Spotify.");
  });

  it("should describe a dance/min-tempo/purchase filter", () => {
    const f = SongFilter.buildFilter(
      "Advanced-ATN,BBA-Dances_desc--S--100-150--"
    );
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        " available on Spotify" +
        " having tempo between 100 and 150 beats per minute" +
        " sorted by Dance Rating from least popular to most popular."
    );
  });

  it("should describe a dance/keyword/min-tempo/purchase filter", () => {
    const f = SongFilter.buildFilter(
      "Advanced-ATN,BBA-Dances_desc-LOVE-S--100-150--"
    );
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        ' containing the text "LOVE"' +
        " available on Spotify" +
        " having tempo between 100 and 150 beats per minute" +
        " sorted by Dance Rating from least popular to most popular."
    );
  });

  it("should describe a complex filter", () => {
    const f = SongFilter.buildFilter(complex);
    expect(f).toBeDefined();
    expect(f).toBeInstanceOf(SongFilter);
    expect(f.isEmpty).toBeFalsy();
    expect(f.description).toEqual(
      "All songs danceable to any of Argentine Tango or Balboa" +
        " available on Amazon" +
        " including tag Big Band" +
        " excluding tag Swing" +
        " having tempo between 100 and 150 beats per minute" +
        " excluding songs disliked by me" +
        " sorted by Dance Rating from most popular to least popular."
    );
  });
});
