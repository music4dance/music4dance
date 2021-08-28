import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Meter, DanceStats, TypeStats } from "../DanceStats";

const rumbaString = `{
            "description": "There are two competition ballroom dances called *Rumba*.  One in the [International Latin] round that based on what the Cubans called bolero-son and is danced in a similar pattern to the American Style [Bolero] and the other in the [American Rhythm] round which is danced to a faster beat and takes a box step as its basic figure.  The American Style Bolero (96-104 BPM), the International Rumba (100-108 BPM), and the American Rumba (120-144 BPM) are all in a subfamily of Latin dance that are danced to similar music at a range of tempi. The *Rhumba* is generally danced as the second dance of [American Rhythm] and third dance of [International Latin] five dance competitions.",
            "songCount": 2632,
            "maxWeight": 10,
            "id": "RMB",
            "name": "Rumba",
            "meter": {
                "numerator": 4,
                "denominator": 4
            },
            "tempoRange": {
                "min": 25.0,
                "max": 36.0
            },
            "organizations": [
                "DanceSport",
                "NDCA"
            ],
            "instances": [
                {
                    "tempoRange": {
                        "min": 30.0,
                        "max": 36.0
                    },
                    "id": "RMBA",
                    "meter": {
                        "numerator": 4,
                        "denominator": 4
                    },
                    "name": "American Rumba",
                    "style": "American Rhythm",
                    "competitionGroup": "Ballroom",
                    "competitionOrder": 2,
                    "exceptions": [
                        {
                            "organization": "NDCA",
                            "tempoRange": {
                                "min": 30.0,
                                "max": 32.0
                            },
                            "competitor": "All",
                            "level": "Bronze"
                        },
                        {
                            "organization": "NDCA",
                            "tempoRange": {
                                "min": 32.0,
                                "max": 36.0
                            },
                            "competitor": "All",
                            "level": "Silver,Gold"
                        },
                        {
                            "organization": "DanceSport",
                            "tempoRange": {
                                "min": 30.0,
                                "max": 34.0
                            },
                            "competitor": "All",
                            "level": "All"
                        }
                    ],
                    "blogTag": null,
                    "cleanName": "american-rumba"
                },
                {
                    "tempoRange": {
                        "min": 25.0,
                        "max": 27.0
                    },
                    "id": "RMBI",
                    "meter": {
                        "numerator": 4,
                        "denominator": 4
                    },
                    "name": "International Rumba",
                    "style": "International Latin",
                    "competitionGroup": "Ballroom",
                    "competitionOrder": 3,
                    "exceptions": [
                        {
                            "organization": "NDCA",
                            "tempoRange": {
                                "min": 27.0,
                                "max": 27.0
                            },
                            "competitor": "ProAm",
                            "level": "All"
                        },
                        {
                            "organization": "NDCA",
                            "tempoRange": {
                                "min": 26.0,
                                "max": 26.0
                            },
                            "competitor": "Professional,Amateur",
                            "level": "All"
                        }
                    ],
                    "blogTag": null,
                    "cleanName": "international-rumba"
                }
            ],
            "groupName": "Latin",
            "blogTag": null,
            "cleanName": "rumba"
        }`;

const boleroString = `{
            "description": "*Bolero* is unique among competitive American Rhythm dances in that it requires not only cuban motion, but contra body movement as found in the smooth dances and rise and fall as is found in the [waltz].   For the purpose of music4dance categorization Bolero is the slowest of the three competition dances that are danced to *Rumba* style music, the other two being the Rumbas from the [International Latin] and [American Rhythm] categories. The Bolero is generally danced as the fourth dance of [American Rhythm five dance] competitions.  ",
            "songCount": 519,
            "maxWeight": 8,
            "id": "BOL",
            "name": "Bolero",
            "meter": {
                "numerator": 4,
                "denominator": 4
            },
            "tempoRange": {
                "min": 24.0,
                "max": 26.0
            },
            "organizations": [
                "DanceSport",
                "NDCA"
            ],
            "instances": [
                {
                    "tempoRange": {
                        "min": 24.0,
                        "max": 26.0
                    },
                    "id": "BOLA",
                    "meter": {
                        "numerator": 4,
                        "denominator": 4
                    },
                    "name": "American Bolero",
                    "style": "American Rhythm",
                    "competitionGroup": "Ballroom",
                    "competitionOrder": 4,
                    "exceptions": [
                        {
                            "organization": "NDCA",
                            "tempoRange": {
                                "min": 24.0,
                                "max": 24.0
                            },
                            "competitor": "All",
                            "level": "Silver,Gold"
                        }
                    ],
                    "blogTag": null,
                    "cleanName": "american-bolero"
                }
            ],
            "groupName": "Latin",
            "blogTag": null,
            "cleanName": "bolero"
        }`;

const chaString = `{
            "description": "The *Cha Cha* is danced to the music of the same name introduced by Cuban composer and violinist Enrique Jordin in 1953. This rhythm was developed from the danzon by a syncopation of the fourth beat. The Cha Cha is danced in 4/4 time and the fourth beat is split in two.  This give the cha cha both its characteristic rhythm (2, 3,4 & 1) and its onomatopoeic name (two  three Cha Cha one). The *Cha Cha* is generally danced as the first dance of [American Rhythm] and second dance of the [International Latin] five dance competitions.",
            "songCount": 11358,
            "maxWeight": 19,
            "id": "CHA",
            "name": "Cha Cha",
            "meter": {
                "numerator": 4,
                "denominator": 4
            },
            "tempoRange": {
                "min": 30.0,
                "max": 31.0
            },
            "organizations": [
                "DanceSport",
                "NDCA"
            ],
            "instances": [
                {
                    "tempoRange": {
                        "min": 30.0,
                        "max": 30.0
                    },
                    "id": "CHAA",
                    "meter": {
                        "numerator": 4,
                        "denominator": 4
                    },
                    "name": "American Cha Cha",
                    "style": "American Rhythm",
                    "competitionGroup": "Ballroom",
                    "competitionOrder": 1,
                    "exceptions": [],
                    "blogTag": null,
                    "cleanName": "american-cha-cha"
                },
                {
                    "tempoRange": {
                        "min": 31.0,
                        "max": 31.0
                    },
                    "id": "CHAI",
                    "meter": {
                        "numerator": 4,
                        "denominator": 4
                    },
                    "name": "International Cha Cha",
                    "style": "International Latin",
                    "competitionGroup": "Ballroom",
                    "competitionOrder": 1,
                    "exceptions": [
                        {
                            "organization": "DanceSport",
                            "tempoRange": {
                                "min": 30.0,
                                "max": 32.0
                            },
                            "competitor": "All",
                            "level": "All"
                        }
                    ],
                    "blogTag": null,
                    "cleanName": "international-cha-cha"
                }
            ],
            "groupName": "Latin",
            "blogTag": null,
            "cleanName": "cha-cha"
        }`;

function loadDance(data: string): TypeStats | undefined {
  const serializer = new TypedJSON(TypeStats);
  const dance = serializer.parse(data);

  return dance;
}

function loadRumba(): TypeStats | undefined {
  return loadDance(rumbaString);
}

function loadBolero(): TypeStats | undefined {
  return loadDance(boleroString);
}

function loadCha(): TypeStats | undefined {
  return loadDance(chaString);
}

describe("dance stats loading", () => {
  it("should load a meter", () => {
    const serializer = new TypedJSON(Meter);
    const meter = serializer.parse('{"numerator": 4, "denominator": 4}');

    expect(meter).toBeDefined();
    expect(meter).toBeInstanceOf(Meter);
    expect(meter?.numerator).toEqual(4);
    expect(meter?.denominator).toEqual(4);
  });

  it("should load a POJO dancestat", () => {
    const rumba = JSON.parse(rumbaString);

    expect(rumba).toBeDefined();
    expect(rumba!.id).toEqual("RMB");
  });

  it("should load a dancestat object", () => {
    const rumba = loadRumba();

    expect(rumba).toBeDefined();
    expect(rumba).toBeInstanceOf(TypeStats);
    expect(rumba!.id).toEqual("RMB");
  });

  it("should filter tempo by style", () => {
    const rumba = loadRumba()!;
    const americanRange = rumba.filteredTempo(["american-rhythm"], []);

    expect(americanRange).toBeDefined();
    expect(americanRange!.min).toEqual(30);
    expect(americanRange!.max).toEqual(36);

    const internationalRange = rumba.filteredTempo(["international-latin"], []);
    expect(internationalRange).toBeDefined();
    expect(internationalRange!.min).toEqual(25);
    expect(internationalRange!.max).toEqual(27);

    const all = rumba!.filteredTempo([], []);
    expect(all).toBeDefined();
    expect(all!.min).toEqual(25);
    expect(all!.max).toEqual(36);

    const several = rumba!.filteredTempo(
      ["american-rhythm", "international-latin", "social"],
      []
    );
    expect(several).toBeDefined();
    expect(several!.min).toEqual(25);
    expect(several!.max).toEqual(36);
  });

  it("should filter tempo by organization (rumba)", () => {
    const rumba = loadRumba()!;

    const americanNDCA1 = rumba!.filteredTempo(["american-rhythm"], ["ndca-1"]);
    expect(americanNDCA1).toBeDefined();
    expect(americanNDCA1!.min).toEqual(30);
    expect(americanNDCA1!.max).toEqual(32);

    const americanNDCA2 = rumba!.filteredTempo(["american-rhythm"], ["ndca-2"]);
    expect(americanNDCA2).toBeDefined();
    expect(americanNDCA2!.min).toEqual(32);
    expect(americanNDCA2!.max).toEqual(36);

    const americanDanceSport = rumba!.filteredTempo(
      ["american-rhythm"],
      ["dancesport"]
    );
    expect(americanDanceSport).toBeDefined();
    expect(americanDanceSport!.min).toEqual(30);
    expect(americanDanceSport!.max).toEqual(34);

    const internationalDanceSport = rumba!.filteredTempo(
      ["international-latin"],
      ["dancesport"]
    );
    expect(internationalDanceSport).toBeDefined();
    expect(internationalDanceSport!.min).toEqual(25);
    expect(internationalDanceSport!.max).toEqual(27);

    const danceSport = rumba!.filteredTempo([], ["dancesport"]);
    expect(danceSport).toBeDefined();
    expect(danceSport!.min).toEqual(25);
    expect(danceSport!.max).toEqual(34);
  });

  it("should filter tempo by organization (bolero)", () => {
    const bolero = loadBolero()!;

    const none = bolero!.filteredTempo([], []);
    expect(none).toBeDefined();
    expect(none!.min).toEqual(24);
    expect(none!.max).toEqual(26);

    const all = bolero!.filteredTempo(
      ["american-rhythm", "international-latin"],
      ["DanceSport", "ndca-1", "ndca-2"]
    );
    expect(all).toBeDefined();
    expect(all!.min).toEqual(24);
    expect(all!.max).toEqual(26);

    const ndca2 = bolero!.filteredTempo([], ["ndca-2"]);
    expect(ndca2).toBeDefined();
    expect(ndca2!.min).toEqual(24);
    expect(ndca2!.max).toEqual(24);
  });

  it("should filter tempo by organization (cha)", () => {
    const cha = loadCha()!;

    const none = cha.filteredTempo([], []);
    expect(none).toBeDefined();
    expect(none!.min).toEqual(30);
    expect(none!.max).toEqual(31);

    const all = cha!.filteredTempo(
      ["american-rhythm", "international-latin"],
      ["DanceSport", "ndca-1", "ndca-2"]
    );
    expect(all).toBeDefined();
    expect(all!.min).toEqual(30);
    expect(all!.max).toEqual(31);

    const danceSport = cha!.filteredTempo(
      ["international-latin"],
      ["dancesport"]
    );
    expect(danceSport).toBeDefined();
    expect(danceSport!.min).toEqual(30);
    expect(danceSport!.max).toEqual(32);
  });
});
