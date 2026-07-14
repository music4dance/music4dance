import { describe, expect, test } from "vitest";
import { DanceFilter } from "../DanceFilter";
import { TypedJSON } from "typedjson";
import { DanceType } from "../DanceType";
import { DanceGroup } from "../DanceGroup";

describe("DanceFilter.ts", () => {
  test("Reduce returns flattened type when orgainzations are covered", () => {
    const dance = createSamba();
    const filter = new DanceFilter({ organizations: ["DanceSport", "NDCA"] });

    const result = filter.reduce(dance)!;

    expect(result).toBeDefined();
    expect(result.instances).toBeDefined();
    expect(result.instances.length).toBe(dance.instances.length);
    expect(dance.meter).toEqual(result.meter);
    expect(dance.tempoRange).toEqual(result.tempoRange);
    for (let i = 0; i < dance.instances.length; i++) {
      const org = dance.instances[i];
      const res = result.instances[i];
      expect(org).toBeDefined();
      expect(res).toBeDefined();

      expect(res!.style).toEqual(org!.style);
      expect(res!.tempoRange).toEqual(org!.tempoRange);
      expect(res!.exceptions.length).toEqual(0);
    }
  });

  test("Reduce narrows .groups to just the selected group(s), like it already narrows instances by style", () => {
    const dance = createSamba();
    dance.groups = [
      new DanceGroup({ internalId: "LAT", internalName: "Latin", danceIds: [] }),
      new DanceGroup({ internalId: "CTY", internalName: "Country", danceIds: [] }),
    ];

    const result = new DanceFilter({ groups: ["Latin"] }).reduce(dance)!;

    expect(result.groups.map((g) => g.name)).toEqual(["Latin"]);
    // the original dance's groups must not be mutated by reduce()
    expect(dance.groups.map((g) => g.name)).toEqual(["Latin", "Country"]);
  });

  test("Reduce leaves .groups untouched when the filter has no group criterion", () => {
    const dance = createSamba();
    dance.groups = [
      new DanceGroup({ internalId: "LAT", internalName: "Latin", danceIds: [] }),
      new DanceGroup({ internalId: "CTY", internalName: "Country", danceIds: [] }),
    ];

    const result = new DanceFilter({}).reduce(dance)!;

    expect(result.groups.map((g) => g.name)).toEqual(["Latin", "Country"]);
  });
});

const smbJson = `
          {
            "id": "SMB",
            "name": "Samba",
            "meter": {
              "numerator": 2,
              "denominator": 4
            },
            "instances": [
              {
                "style": "International Latin",
                "organizations": [
                  "DanceSport",
                  "NDCA"
                ],
                "tempoRange": {
                  "min": 100.0,
                  "max": 104.0
                },
                "competitionGroup": "Ballroom",
                "competitionOrder": 2,
                "exceptions": [
                  {
                    "organization": "NDCA",
                    "tempoRange": {
                      "min": 100.0,
                      "max": 100.0
                    }
                  }
                ]
              },
              {
                "style": "American Rhythm",
                "organizations": [
                  "DanceSport",
                  "NDCA"
                ],
                "tempoRange": {
                  "min": 96.0,
                  "max": 100.0
                },
                "competitionGroup": "Ballroom",
                "exceptions": [
                  {
                    "organization": "NDCA",
                    "tempoRange": {
                      "min": 100.0,
                      "max": 100.0
                    }
                  },
                  {
                    "organization": "DanceSport",
                    "tempoRange": {
                      "min": 96.0,
                      "max": 96.0
                    }
                  }
                ]
              }
            ]
          }
`;

function createSamba(): DanceType {
  return TypedJSON.parse(smbJson, DanceType)!;
}
