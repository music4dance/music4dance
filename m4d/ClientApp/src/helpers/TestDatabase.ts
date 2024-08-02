import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";

const dances = [
  {
    id: "SWZ",
    name: "Slow Waltz",
    meter: {
      numerator: 3,
      denominator: 4,
    },
    blogTag: "waltz",
    synonyms: ["Waltz"],
    organizations: ["DanceSport", "NDCA"],
    instances: [
      {
        tempoRange: {
          min: 84.0,
          max: 90.0,
        },
        meter: {
          numerator: 3,
          denominator: 4,
        },
        style: "American Smooth",
        competitionGroup: "Ballroom",
        competitionOrder: 1,
        exceptions: [
          {
            organization: "NDCA",
            tempoRange: {
              min: 90.0,
              max: 96.0,
            },
          },
        ],
      },
      {
        tempoRange: {
          min: 84.0,
          max: 90.0,
        },
        meter: {
          numerator: 3,
          denominator: 4,
        },
        style: "International Standard",
        competitionGroup: "Ballroom",
        competitionOrder: 1,
        exceptions: [
          {
            organization: "NDCA",
            tempoRange: {
              min: 84.0,
              max: 84.0,
            },
          },
        ],
      },
    ],
  },
  {
    id: "SFT",
    name: "Slow Foxtrot",
    meter: {
      numerator: 4,
      denominator: 4,
    },
    blogTag: "slow-foxtrot",
    synonyms: ["Foxtrot"],
    organizations: ["DanceSport", "NDCA"],
    instances: [
      {
        tempoRange: {
          min: 120.0,
          max: 136.0,
        },
        meter: {
          numerator: 4,
          denominator: 4,
        },
        style: "American Smooth",
        competitionGroup: "Ballroom",
        competitionOrder: 3,
        exceptions: [
          {
            organization: "NDCA",
            tempoRange: {
              min: 128.0,
              max: 136.0,
            },
          },
          {
            organization: "NDCA",
            tempoRange: {
              min: 120.0,
              max: 120.0,
            },
          },
          {
            organization: "DanceSport",
            tempoRange: {
              min: 120.0,
              max: 128.0,
            },
          },
        ],
      },
      {
        tempoRange: {
          min: 112.0,
          max: 120.0,
        },
        meter: {
          numerator: 4,
          denominator: 4,
        },
        style: "International Standard",
        competitionGroup: "Ballroom",
        competitionOrder: 4,
      },
    ],
  },
];

const groups = [
  {
    id: "FOO",
    name: "FooBar",
    danceIds: ["SWZ", "SFT"],
  },
];

export function loadDatabase(): DanceDatabase {
  const db = { dances: dances, groups: groups };
  return DanceDatabase.load(JSON.stringify(db));
}
