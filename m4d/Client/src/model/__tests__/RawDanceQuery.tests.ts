import "reflect-metadata";
import { fetchEnvironment } from "../DanceEnvironmet";
import { DanceQuery } from "../DanceQuery";
import { RawDanceQuery } from "../RawDanceQuery";

declare const global: any;

beforeAll(() => {
  global.environment = fetchEnvironment();
});

describe("raw dance query", () => {
  it("should construct", () => {
    const dq = new RawDanceQuery(
      "DanceTags/any(t: t eq 'Rumba')",
      "singleDance"
    );
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(RawDanceQuery);
  });

  it("should handle single dance", () => {
    const dq = new RawDanceQuery(
      "DanceTags/any(t: t eq 'Rumba')",
      "singleDance"
    );
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(RawDanceQuery);
    expect(dq.singleDance).toBeTruthy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(1);
    expect(dq.danceList[0]).toEqual("RMB");
  });

  it("should handle multiple flags", () => {
    const dq = new RawDanceQuery(
      "DanceTags/any(t: t eq 'Rumba')",
      "singleDance|otherflag"
    );
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(RawDanceQuery);
    expect(dq.singleDance).toBeTruthy();
  });

  it("should handle empty dance", () => {
    const dq = new RawDanceQuery();
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(RawDanceQuery);
    expect(dq.singleDance).toBeFalsy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(0);
  });

  it("should handle multiple flags", () => {
    // Eventually we may want to make this case work for real - for now
    //  just making sure it doesn't return the singleDance flag
    const dq = new RawDanceQuery(
      "DanceTags/any(t: t eq 'Rumba' or t eq 'Carolina Shag')",
      "otherflag"
    );
    expect(dq).toBeDefined();
    expect(dq).toBeInstanceOf(RawDanceQuery);
    expect(dq.singleDance).toBeFalsy();
    expect(dq.danceList).toBeDefined();
    expect(dq.danceList.length).toEqual(1);
    expect(dq.danceList[0]).toEqual("RMB");
  });
});
