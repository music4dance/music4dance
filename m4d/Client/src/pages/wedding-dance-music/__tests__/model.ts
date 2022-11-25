export const model = {
  columns: [
    {
      title: "Wedding",
      tag: "Wedding:Other",
    },
    {
      title: "First Dance",
      tag: "First Dance:Other",
    },
    {
      title: "Mother/Son",
      tag: "Mother Son:Other",
    },
    {
      title: "Father/Daughter",
      tag: "Father Daughter:Other",
    },
  ],
  groups: [
    {
      children: [
        {
          dance: {
            id: "BBA",
            name: "Balboa",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            tempoRange: {
              min: 160,
              max: 260,
            },
            instances: [
              {
                tempoRange: {
                  min: 160,
                  max: 260,
                },
                id: "BBAS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Balboa",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [1, 1, 0, 0],
        },
        {
          dance: {
            id: "CSG",
            name: "Carolina Shag",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "carolina-shag",
            tempoRange: {
              min: 108,
              max: 132,
            },
            instances: [
              {
                tempoRange: {
                  min: 108,
                  max: 132,
                },
                id: "CSGS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Carolina Shag",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [17, 17, 3, 3],
        },
        {
          dance: {
            id: "CST",
            name: "Charleston",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "charleston",
            tempoRange: {
              min: 200,
              max: 300,
            },
            instances: [
              {
                tempoRange: {
                  min: 200,
                  max: 300,
                },
                id: "CSTS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Charleston",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [3, 4, 0, 0],
        },
        {
          dance: {
            id: "ECS",
            name: "East Coast Swing",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "east-coast-swing",
            synonyms: ["Triple Swing", "East Coast"],
            searchonyms: ["swing", "ec swing"],
            tempoRange: {
              min: 136,
              max: 144,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [95, 89, 16, 16],
        },
        {
          dance: {
            id: "HST",
            name: "Hustle",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "hustle",
            synonyms: ["Street Swing", "Disco Fox"],
            tempoRange: {
              min: 112,
              max: 120,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [37, 34, 5, 5],
        },
        {
          dance: {
            id: "JIV",
            name: "Jive",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "jive",
            tempoRange: {
              min: 152,
              max: 176,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
          },
          counts: [30, 30, 6, 6],
        },
        {
          dance: {
            id: "JSW",
            name: "Jump Swing",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            tempoRange: {
              min: 160,
              max: 240,
            },
            instances: [
              {
                tempoRange: {
                  min: 160,
                  max: 240,
                },
                id: "JSWS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Jump Swing",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [2, 2, 0, 0],
        },
        {
          dance: {
            id: "WCS",
            name: "West Coast Swing",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "west-coast-swing",
            synonyms: ["West Coast"],
            searchonyms: ["wc swing"],
            tempoRange: {
              min: 112,
              max: 128,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [126, 121, 8, 10],
        },
        {
          dance: {
            id: "LHP",
            name: "Lindy Hop",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "lindy-hop",
            synonyms: ["Swing", "Jitterbug"],
            tempoRange: {
              min: 120,
              max: 180,
            },
            instances: [
              {
                tempoRange: {
                  min: 120,
                  max: 180,
                },
                id: "LHPS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Lindy Hop",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [47, 45, 4, 4],
        },
      ],
      dance: {
        id: "SWG",
        name: "Swing",
        meter: {
          numerator: 4,
          denominator: 4,
        },
        tempoRange: {
          min: 108,
          max: 300,
        },
        danceIds: [
          "BBA",
          "CLS",
          "CSG",
          "CST",
          "ECS",
          "HST",
          "JIV",
          "JSW",
          "WCS",
          "LHP",
        ],
        blogTag: "swing",
      },
      counts: [358, 343, 42, 44],
    },
    {
      children: [
        {
          dance: {
            id: "TGO",
            name: "Tango (Ballroom)",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "tango",
            tempoRange: {
              min: 120,
              max: 128,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [13, 13, 0, 0],
        },
        {
          dance: {
            id: "ATN",
            name: "Argentine Tango",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "tango",
            synonyms: ["Tango Argentino"],
            tempoRange: {
              min: 112,
              max: 140,
            },
            instances: [
              {
                tempoRange: {
                  min: 112,
                  max: 140,
                },
                id: "ATNS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Argentine Tango",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [3, 3, 0, 0],
        },
        {
          dance: {
            id: "NTN",
            name: "Neo Tango",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "tango",
            tempoRange: {
              min: 60,
              max: 100,
            },
            instances: [
              {
                tempoRange: {
                  min: 60,
                  max: 100,
                },
                id: "NTNS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Neo Tango",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [2, 2, 0, 0],
        },
        {
          dance: {
            id: "MGA",
            name: "Milonga",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "milonga",
            tempoRange: {
              min: 176,
              max: 224,
            },
            instances: [
              {
                tempoRange: {
                  min: 176,
                  max: 224,
                },
                id: "MGAS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Milonga",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [1, 1, 1, 1],
        },
        {
          dance: {
            id: "TGV",
            name: "Tango Vals",
            meter: {
              numerator: 3,
              denominator: 4,
            },
            blogTag: "tango-vals",
            tempoRange: {
              min: 150,
              max: 240,
            },
            instances: [
              {
                tempoRange: {
                  min: 150,
                  max: 240,
                },
                id: "TGVS",
                meter: {
                  numerator: 3,
                  denominator: 4,
                },
                name: "Social Tango Vals",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [2, 2, 0, 0],
        },
      ],
      dance: {
        id: "TNG",
        name: "Tango",
        meter: {
          numerator: 4,
          denominator: 4,
        },
        tempoRange: {
          min: 60,
          max: 240,
        },
        danceIds: ["TGO", "ATN", "NTN", "MGA", "TGV"],
        blogTag: "tango",
      },
      counts: [21, 21, 1, 1],
    },
    {
      children: [
        {
          dance: {
            id: "CSW",
            name: "Cross-step Waltz",
            meter: {
              numerator: 3,
              denominator: 4,
            },
            blogTag: "waltz",
            tempoRange: {
              min: 108,
              max: 120,
            },
            instances: [
              {
                tempoRange: {
                  min: 108,
                  max: 120,
                },
                id: "CSWS",
                meter: {
                  numerator: 3,
                  denominator: 4,
                },
                name: "Social Cross-step Waltz",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [4, 3, 1, 1],
        },
        {
          dance: {
            id: "SWZ",
            name: "Slow Waltz",
            meter: {
              numerator: 3,
              denominator: 4,
            },
            blogTag: "waltz",
            synonyms: ["Waltz"],
            tempoRange: {
              min: 84,
              max: 90,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [101, 93, 13, 18],
        },
        {
          dance: {
            id: "VWZ",
            name: "Viennese Waltz",
            meter: {
              numerator: 3,
              denominator: 4,
            },
            blogTag: "waltz",
            searchonyms: ["viennese"],
            tempoRange: {
              min: 159,
              max: 174,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [102, 95, 12, 13],
        },
        {
          dance: {
            id: "TGV",
            name: "Tango Vals",
            meter: {
              numerator: 3,
              denominator: 4,
            },
            blogTag: "tango-vals",
            tempoRange: {
              min: 150,
              max: 240,
            },
            instances: [
              {
                tempoRange: {
                  min: 150,
                  max: 240,
                },
                id: "TGVS",
                meter: {
                  numerator: 3,
                  denominator: 4,
                },
                name: "Social Tango Vals",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [2, 2, 0, 0],
        },
      ],
      dance: {
        id: "WLZ",
        name: "Waltz",
        meter: {
          numerator: 3,
          denominator: 4,
        },
        tempoRange: {
          min: 84,
          max: 240,
        },
        danceIds: ["CSW", "SWZ", "VWZ", "TGV"],
        blogTag: "waltz",
      },
      counts: [209, 193, 26, 32],
    },
    {
      children: [
        {
          dance: {
            id: "CFT",
            name: "Castle Foxtrot",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "castle-foxtrot",
            synonyms: ["Slow Dance"],
            tempoRange: {
              min: 60,
              max: 100,
            },
            instances: [
              {
                tempoRange: {
                  min: 60,
                  max: 100,
                },
                id: "CFTS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Castle Foxtrot",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [319, 293, 48, 50],
        },
        {
          dance: {
            id: "SFT",
            name: "Slow Foxtrot",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "slow-foxtrot",
            synonyms: ["Foxtrot"],
            tempoRange: {
              min: 112,
              max: 136,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [166, 156, 16, 19],
        },
        {
          dance: {
            id: "QST",
            name: "Quickstep",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "quickstep",
            tempoRange: {
              min: 192,
              max: 208,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
          },
          counts: [15, 15, 0, 0],
        },
        {
          dance: {
            id: "PBD",
            name: "Peabody",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "peabody",
            synonyms: ["One Step"],
            tempoRange: {
              min: 240,
              max: 248,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
          counts: [1, 1, 0, 0],
        },
      ],
      dance: {
        id: "FXT",
        name: "Foxtrot",
        meter: {
          numerator: 4,
          denominator: 4,
        },
        tempoRange: {
          min: 60,
          max: 248,
        },
        danceIds: ["CFT", "SFT", "QST", "PBD"],
        blogTag: "foxtrot",
      },
      counts: [501, 465, 64, 69],
    },
    {
      children: [
        {
          dance: {
            id: "BOL",
            name: "Bolero",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "bolero",
            tempoRange: {
              min: 96,
              max: 104,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [63, 58, 9, 9],
        },
        {
          dance: {
            id: "CHA",
            name: "Cha Cha",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "cha-cha",
            tempoRange: {
              min: 120,
              max: 124,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [113, 106, 19, 19],
        },
        {
          dance: {
            id: "MBO",
            name: "Mambo",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "mambo",
            tempoRange: {
              min: 188,
              max: 204,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
          },
          counts: [13, 13, 0, 0],
        },
        {
          dance: {
            id: "RMB",
            name: "Rumba",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "rumba",
            tempoRange: {
              min: 100,
              max: 144,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [255, 243, 30, 31],
        },
        {
          dance: {
            id: "SMB",
            name: "Samba",
            meter: {
              numerator: 2,
              denominator: 4,
            },
            blogTag: "samba",
            tempoRange: {
              min: 96,
              max: 104,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [11, 10, 3, 3],
        },
        {
          dance: {
            id: "SLS",
            name: "Salsa",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "salsa",
            tempoRange: {
              min: 160,
              max: 220,
            },
            instances: [
              {
                tempoRange: {
                  min: 160,
                  max: 220,
                },
                id: "SLSS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Salsa",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [17, 16, 0, 0],
        },
        {
          dance: {
            id: "BCH",
            name: "Bachata",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "bachata",
            tempoRange: {
              min: 108,
              max: 152,
            },
            instances: [
              {
                tempoRange: {
                  min: 108,
                  max: 152,
                },
                id: "BCHS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Bachata",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [5, 4, 2, 2],
        },
        {
          dance: {
            id: "CMB",
            name: "Cumbia",
            meter: {
              numerator: 2,
              denominator: 4,
            },
            tempoRange: {
              min: 90,
              max: 110,
            },
            instances: [
              {
                tempoRange: {
                  min: 90,
                  max: 110,
                },
                id: "CMBS",
                meter: {
                  numerator: 2,
                  denominator: 4,
                },
                name: "Social Cumbia",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [3, 2, 1, 1],
        },
        {
          dance: {
            id: "MRG",
            name: "Merengue",
            meter: {
              numerator: 2,
              denominator: 4,
            },
            tempoRange: {
              min: 58,
              max: 64,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
            ],
          },
          counts: [8, 8, 2, 2],
        },
      ],
      dance: {
        id: "LTN",
        name: "Latin",
        meter: {
          numerator: 4,
          denominator: 4,
        },
        tempoRange: {
          min: 58,
          max: 220,
        },
        danceIds: [
          "BOL",
          "BSN",
          "CHA",
          "MBO",
          "RMB",
          "PDL",
          "SMB",
          "SLS",
          "BCH",
          "CMB",
          "MRG",
        ],
      },
      counts: [488, 460, 66, 67],
    },
    {
      children: [
        {
          dance: {
            id: "BLU",
            name: "Blues",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            blogTag: "blues",
            tempoRange: {
              min: 40,
              max: 100,
            },
            instances: [
              {
                tempoRange: {
                  min: 40,
                  max: 100,
                },
                id: "BLUS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Blues",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [28, 26, 4, 5],
        },
        {
          dance: {
            id: "C2S",
            name: "Country Two Step",
            meter: {
              numerator: 2,
              denominator: 4,
            },
            blogTag: "country-two-step",
            tempoRange: {
              min: 168,
              max: 200,
            },
            instances: [
              {
                tempoRange: {
                  min: 168,
                  max: 200,
                },
                id: "C2SS",
                meter: {
                  numerator: 2,
                  denominator: 4,
                },
                name: "Social Country Two Step",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [4, 3, 0, 1],
        },
        {
          dance: {
            id: "MWT",
            name: "Motown",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            tempoRange: {
              min: 80,
              max: 150,
            },
            instances: [
              {
                tempoRange: {
                  min: 80,
                  max: 150,
                },
                id: "MWTS",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Motown",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [16, 16, 6, 6],
        },
        {
          dance: {
            id: "NC2",
            name: "Night Club Two Step",
            meter: {
              numerator: 4,
              denominator: 4,
            },
            searchonyms: ["club two step"],
            tempoRange: {
              min: 50,
              max: 92,
            },
            instances: [
              {
                tempoRange: {
                  min: 50,
                  max: 92,
                },
                id: "NC2S",
                meter: {
                  numerator: 4,
                  denominator: 4,
                },
                name: "Social Night Club Two Step",
                style: "Social",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [96, 87, 9, 14],
        },
        {
          dance: {
            id: "PLK",
            name: "Polka",
            meter: {
              numerator: 2,
              denominator: 4,
            },
            blogTag: "polka",
            tempoRange: {
              min: 120,
              max: 124,
            },
            organizations: ["DanceSport", "NDCA"],
            instances: [
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
          counts: [1, 1, 0, 0],
        },
      ],
      dance: {
        id: "MSC",
        name: "Other",
        meter: {
          numerator: 4,
          denominator: 4,
        },
        tempoRange: {
          min: 40,
          max: 200,
        },
        danceIds: ["BLU", "C2S", "MWT", "NC2", "PLK"],
      },
      counts: [145, 133, 19, 26],
    },
    {
      children: [
        {
          dance: {
            id: "CNT",
            name: "Contemporary",
            meter: {
              numerator: 1,
              denominator: 1,
            },
            tempoRange: {
              min: 1,
              max: 500,
            },
            instances: [
              {
                tempoRange: {
                  min: 1,
                  max: 500,
                },
                id: "CNTP",
                meter: {
                  numerator: 1,
                  denominator: 1,
                },
                name: "Performance Contemporary",
                style: "Performance",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [11, 9, 2, 2],
        },
        {
          dance: {
            id: "FST",
            name: "Freestyle",
            meter: {
              numerator: 1,
              denominator: 1,
            },
            blogTag: "disco",
            tempoRange: {
              min: 1,
              max: 500,
            },
            instances: [
              {
                tempoRange: {
                  min: 1,
                  max: 500,
                },
                id: "FSTP",
                meter: {
                  numerator: 1,
                  denominator: 1,
                },
                name: "Performance Freestyle",
                style: "Performance",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [3, 3, 0, 0],
        },
        {
          dance: {
            id: "JAZ",
            name: "Jazz",
            meter: {
              numerator: 1,
              denominator: 1,
            },
            blogTag: "jazz",
            tempoRange: {
              min: 1,
              max: 500,
            },
            instances: [
              {
                tempoRange: {
                  min: 1,
                  max: 500,
                },
                id: "JAZP",
                meter: {
                  numerator: 1,
                  denominator: 1,
                },
                name: "Performance Jazz",
                style: "Performance",
                competitionOrder: 0,
                exceptions: [],
              },
            ],
          },
          counts: [7, 5, 2, 2],
        },
      ],
      dance: {
        id: "PRF",
        name: "Performance",
        meter: {
          numerator: 1,
          denominator: 1,
        },
        tempoRange: {
          min: 1,
          max: 500,
        },
        danceIds: [
          "BDW",
          "BLT",
          "BWD",
          "CNT",
          "DSC",
          "FST",
          "HHP",
          "JAZ",
          "TAP",
        ],
      },
      counts: [21, 17, 4, 4],
    },
  ],
};
