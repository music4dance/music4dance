import { DanceRatingDelta } from "@/DanceRatingDelta";

describe("dance rating delta serialize/deserialize", () => {
  it("should parse valid positive drds", () => {
    const pos = DanceRatingDelta.fromString("LTN+37");
    expect(pos).toBeDefined();
    expect(pos).toBeInstanceOf(DanceRatingDelta);
    expect(pos.danceId).toEqual("LTN");
    expect(pos.delta).toEqual(37);
  });

  it("should parse valid negative drds", () => {
    const pos = DanceRatingDelta.fromString("LTN-5");
    expect(pos).toBeDefined();
    expect(pos).toBeInstanceOf(DanceRatingDelta);
    expect(pos.danceId).toEqual("LTN");
    expect(pos.delta).toEqual(-5);
  });

  it("should throw on falsey", () => {
    expect(() => {
      DanceRatingDelta.fromString("");
    }).toThrowError("value must not be falsey");
  });

  it("should throw on misformatted string", () => {
    const error = "must conform to {dancid}(+|-){count}";
    expect(() => {
      DanceRatingDelta.fromString("LTN");
    }).toThrowError(error);
    expect(() => {
      DanceRatingDelta.fromString("LTN+");
    }).toThrowError(error);
    expect(() => {
      DanceRatingDelta.fromString("LTN!34");
    }).toThrowError(error);
    expect(() => {
      DanceRatingDelta.fromString("LTN+SWG");
    }).toThrowError(error);
  });

  it("should format positive drds", () => {
    expect(new DanceRatingDelta("LTN", 32).toString()).toEqual("LTN+32");
    expect(new DanceRatingDelta("LTN", 0).toString()).toEqual("LTN+0");
  });

  it("should format negative drds", () => {
    expect(new DanceRatingDelta("LTN", -1).toString()).toEqual("LTN-1");
    expect(new DanceRatingDelta("LTN", -512).toString()).toEqual("LTN-512");
  });
});
