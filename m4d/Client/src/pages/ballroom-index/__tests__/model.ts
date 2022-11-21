export const model = {
  name: "Ballroom",
  categories: [
    {
      name: "American Smooth",
      group: "Ballroom",
      categoryType: "American",
      canonicalName: "american-smooth",
      fullRoundName: "American Smooth four dance round",
      round: [
        {
          tempoRange: {
            min: 84,
            max: 90,
          },
          id: "SWZA",
          meter: {
            numerator: 3,
            denominator: 4,
          },
          name: "American Slow Waltz",
          style: "American Smooth",
          competitionGroup: "Ballroom",
          competitionOrder: 1,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 90,
                max: 96,
              },
              competitor: "All",
              level: "Bronze",
            },
          ],
        },
        {
          tempoRange: {
            min: 120,
            max: 120,
          },
          id: "TGOA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Tango (Ballroom)",
          style: "American Smooth",
          competitionGroup: "Ballroom",
          competitionOrder: 2,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 120,
                max: 128,
              },
              competitor: "All",
              level: "Bronze",
            },
          ],
        },
        {
          tempoRange: {
            min: 120,
            max: 136,
          },
          id: "SFTA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Slow Foxtrot",
          style: "American Smooth",
          competitionGroup: "Ballroom",
          competitionOrder: 3,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 128,
                max: 136,
              },
              competitor: "All",
              level: "Bronze",
            },
            {
              organization: "NDCA",
              tempoRange: {
                min: 120,
                max: 120,
              },
              competitor: "All",
              level: "Silver,Gold",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 120,
                max: 128,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 159,
            max: 162,
          },
          id: "VWZA",
          meter: {
            numerator: 3,
            denominator: 4,
          },
          name: "American Viennese Waltz",
          style: "American Smooth",
          competitionGroup: "Ballroom",
          competitionOrder: 4,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 162,
                max: 162,
              },
              competitor: "All",
              level: "Bronze",
            },
          ],
        },
      ],
      extras: [
        {
          tempoRange: {
            min: 240,
            max: 248,
          },
          id: "PBDA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Peabody",
          style: "American Smooth",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
      ],
    },
    {
      name: "International Standard",
      group: "Ballroom",
      categoryType: "International",
      canonicalName: "international-standard",
      fullRoundName: "International Standard five dance round",
      round: [
        {
          tempoRange: {
            min: 84,
            max: 90,
          },
          id: "SWZI",
          meter: {
            numerator: 3,
            denominator: 4,
          },
          name: "International Slow Waltz",
          style: "International Standard",
          competitionGroup: "Ballroom",
          competitionOrder: 1,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 84,
                max: 84,
              },
              competitor: "Professional,Amateur",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 128,
            max: 128,
          },
          id: "TGOI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Tango (Ballroom)",
          style: "International Standard",
          competitionGroup: "Ballroom",
          competitionOrder: 2,
          exceptions: [
            {
              organization: "DanceSport",
              tempoRange: {
                min: 124,
                max: 132,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 168,
            max: 174,
          },
          id: "VWZI",
          meter: {
            numerator: 3,
            denominator: 4,
          },
          name: "International Viennese Waltz",
          style: "International Standard",
          competitionGroup: "Ballroom",
          competitionOrder: 3,
          exceptions: [
            {
              organization: "DanceSport",
              tempoRange: {
                min: 174,
                max: 180,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 112,
            max: 120,
          },
          id: "SFTI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Slow Foxtrot",
          style: "International Standard",
          competitionGroup: "Ballroom",
          competitionOrder: 4,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 192,
            max: 208,
          },
          id: "QSTI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Quickstep",
          style: "International Standard",
          competitionGroup: "Ballroom",
          competitionOrder: 5,
          exceptions: [
            {
              organization: "DanceSport",
              tempoRange: {
                min: 200,
                max: 208,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
      ],
      extras: [],
    },
    {
      name: "American Rhythm",
      group: "Ballroom",
      categoryType: "American",
      canonicalName: "american-rhythm",
      fullRoundName: "American Rhythm five dance round",
      round: [
        {
          tempoRange: {
            min: 120,
            max: 120,
          },
          id: "CHAA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Cha Cha",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 1,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 120,
            max: 144,
          },
          id: "RMBA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Rumba",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 2,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 120,
                max: 128,
              },
              competitor: "All",
              level: "Bronze",
            },
            {
              organization: "NDCA",
              tempoRange: {
                min: 128,
                max: 144,
              },
              competitor: "All",
              level: "Silver,Gold",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 120,
                max: 136,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 136,
            max: 144,
          },
          id: "ECSA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American East Coast Swing",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 3,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 144,
                max: 144,
              },
              competitor: "All",
              level: "Silver,Gold",
            },
          ],
        },
        {
          tempoRange: {
            min: 96,
            max: 104,
          },
          id: "BOLA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Bolero",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 4,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 96,
                max: 96,
              },
              competitor: "All",
              level: "Silver,Gold",
            },
          ],
        },
        {
          tempoRange: {
            min: 188,
            max: 204,
          },
          id: "MBOA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Mambo",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 5,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 192,
                max: 204,
              },
              competitor: "All",
              level: "Bronze",
            },
            {
              organization: "NDCA",
              tempoRange: {
                min: 188,
                max: 188,
              },
              competitor: "All",
              level: "Silver,Gold",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 188,
                max: 204,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
      ],
      extras: [
        {
          tempoRange: {
            min: 112,
            max: 128,
          },
          id: "WCSA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American West Coast Swing",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 112,
            max: 120,
          },
          id: "HSTA",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "American Hustle",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 58,
            max: 64,
          },
          id: "MRGA",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "American Merengue",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 116,
            max: 120,
          },
          id: "PDLA",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "American Paso Doble",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 96,
            max: 104,
          },
          id: "SMBA",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "American Samba",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 104,
                max: 104,
              },
              competitor: "All",
              level: "All",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 96,
                max: 96,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 120,
            max: 124,
          },
          id: "PLKA",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "American Polka",
          style: "American Rhythm",
          competitionGroup: "Ballroom",
          competitionOrder: 0,
          exceptions: [],
        },
      ],
    },
    {
      name: "International Latin",
      group: "Ballroom",
      categoryType: "International",
      canonicalName: "international-latin",
      fullRoundName: "International Latin five dance round",
      round: [
        {
          tempoRange: {
            min: 124,
            max: 124,
          },
          id: "CHAI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Cha Cha",
          style: "International Latin",
          competitionGroup: "Ballroom",
          competitionOrder: 1,
          exceptions: [
            {
              organization: "DanceSport",
              tempoRange: {
                min: 120,
                max: 128,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 96,
            max: 104,
          },
          id: "SMBI",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "International Samba",
          style: "International Latin",
          competitionGroup: "Ballroom",
          competitionOrder: 2,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 100,
                max: 100,
              },
              competitor: "Professional,Amateur",
              level: "All",
            },
            {
              organization: "NDCA",
              tempoRange: {
                min: 96,
                max: 100,
              },
              competitor: "ProAm",
              level: "All",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 100,
                max: 104,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 100,
            max: 108,
          },
          id: "RMBI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Rumba",
          style: "International Latin",
          competitionGroup: "Ballroom",
          competitionOrder: 3,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 108,
                max: 108,
              },
              competitor: "ProAm",
              level: "All",
            },
            {
              organization: "NDCA",
              tempoRange: {
                min: 104,
                max: 104,
              },
              competitor: "Professional,Amateur",
              level: "All",
            },
          ],
        },
        {
          tempoRange: {
            min: 120,
            max: 124,
          },
          id: "PDLI",
          meter: {
            numerator: 2,
            denominator: 4,
          },
          name: "International Paso Doble",
          style: "International Latin",
          competitionGroup: "Ballroom",
          competitionOrder: 4,
          exceptions: [],
        },
        {
          tempoRange: {
            min: 152,
            max: 176,
          },
          id: "JIVI",
          meter: {
            numerator: 4,
            denominator: 4,
          },
          name: "International Jive",
          style: "International Latin",
          competitionGroup: "Ballroom",
          competitionOrder: 5,
          exceptions: [
            {
              organization: "NDCA",
              tempoRange: {
                min: 176,
                max: 176,
              },
              competitor: "Professional,Amateur",
              level: "All",
            },
            {
              organization: "DanceSport",
              tempoRange: {
                min: 168,
                max: 176,
              },
              competitor: "All",
              level: "All",
            },
          ],
        },
      ],
      extras: [],
    },
  ],
};
