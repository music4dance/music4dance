import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Meter, DanceStats } from "../DanceStats";

const rumbaString = `{
    "danceId": "RMB",
    "danceName": "Rumba",
    "blogTag": "rumba",
    "description": "There are two competition ballroom dances called *Rumba*.  One in the [International Latin] round that based on what the Cubans called bolero-son and is danced in a similar pattern to the American Style [Bolero] and the other in the [American Rhythm] round which is danced to a faster beat and takes a box step as its basic figure.  The American Style Bolero (96-104 BPM), the International Rumba (100-108 BPM), and the American Rumba (120-144 BPM) are all in a subfamily of Latin dance that are danced to similar music at a range of tempi. The *Rhumba* is generally danced as the second dance of [American Rhythm] and third dance of [International Latin] five dance competitions.",
    "songCount": 2322,
    "songCountExplicit": 2322,
    "maxWeight": 16,
    "songTags": "1940S:Other:6|1950S:Other:9|1960S:Other:16|1970S:Other:22|1980S:Other:32|1990S:Other:21|2/4:Tempo:1|2000S:Other:20|2010S:Other:22|2013:Other:1|2015:Other:29|3/4:Tempo:34|4/4:Tempo:2128|Adult Alternative:Music:13|Adult Contemporary:Music:47|African:Music:4|Alan:Other:2|Alek:Other:4|Alexa:Other:3|Allison:Other:6|Alternative:Music:154|Alternative:Style:5|Alternativo & Rock Latino:Music:2|Always:Other:1|Ambient:Music:37|American:Style:565|Andante:Tempo:1|Andy:Other:4|Antonio:Other:2|Artem:Other:4|Asian:Music:2|Baladas Y Boleros:Music:26|Big Band:Music:5|Bindi:Other:3|Bluegrass:Music:5|Blues / Folk:Music:9|Blues:Music:20|Bobby:Other:1|Bonner:Other:3|Bossa Nova:Music:17|Brandon:Other:1|Brazilian:Music:18|Britpop:Music:2|Brittany:Other:1|Calvin:Other:1|Caribbean:Music:7|Carlos:Other:3|Carrie Ann Inaba:Other:1|Charlotte:Other:1|Cheryl:Other:3|Children's Music:Music:7|Childrens:Music:8|Chris:Other:1|Christian / Gospel:Music:8|Christmas Hits:Other:1|Christmas:Music:4|Christmas:Other:1|Classic:Other:1|Classical:Music:17|Club:Style:16|Comedy / Spoken Word:Music:6|Comedy:Music:4|Compilations:Music:3|Contemporary Christmas:Other:1|Contemporary Country:Music:3|Contemporary Latin:Music:4|Contemporary R&B:Music:7|Contemporary Singer/Songwriter:Music:2|Contemporary:Music:10|Contemporary:Style:12|Country:Music:88|Dance:Music:58|Debbie:Other:1|Derek:Other:5|Disco:Music:2|Doo Wop:Music:3|Downtempo:Music:4|Drew:Other:1|Dwts:Other:80|Easy Listening:Music:50|Electronic / Dance:Music:74|Electronic:Music:91|Emma:Other:4|English:Other:1|Episode 10:Other:7|Episode 11:Other:4|Episode 1:Other:2|Episode 2:Other:15|Episode 3:Other:8|Episode 4:Other:10|Episode 5:Other:8|Episode 6:Other:10|Episode 7:Other:3|Episode 8:Other:7|Episode 9:Other:6|Erika:Other:1|Euro Pop:Music:4|European:Music:11|Evanna:Other:1|Faithful:Other:1|Fake:Tempo:27|Fast:Tempo:6|Father Daughter:Other:23|First Dance:Other:225|Flamenco:Music:3|Folk:Music:17|French Pop:Music:2|Funk:Music:4|Gleb:Other:2|Hard Rock:Music:2|Heart:Other:3|Hip Hop/Rap:Music:43|Hip Hop:Music:15|Holiday:Music:17|Holiday:Other:17|Hopeless Romantics:Other:25|House:Music:2|Instrumental:Music:15|Instrumental:Other:5|International:Music:132|International:Style:962|James:Other:2|Jazz:Music:242|Keo:Other:3|Kids:Music:7|Last Dance:Other:14|Latin Alternative & Rock:Music:2|Latin Jazz:Music:8|Latin Pop:Music:16|Latin:Music:410|Laurie:Other:2|Lindsay:Other:5|Long Intro:Tempo:1|Louis:Other:2|Lounge:Music:4|Maks:Other:2|Mandopop:Music:2|Mark:Other:8|Measure:Tempo:3|Miscellaneous Audio Recording:Music:12|Modern:Style:29|More:Music:18|Mother Son:Other:19|Motown:Music:11|Musicals:Music:2|Música Mexicana:Music:2|Música Tropical:Music:19|Nastia:Other:2|New Age:Music:13|Nick:Other:5|Noah:Other:3|Normani:Other:2|Oldies:Music:6|Opera:Music:8|Paige:Other:4|Patti:Other:2|Peta:Other:5|Pop In Spanish:Music:13|Pop Latino:Music:92|Pop/Rock:Music:7|Pop:Music:1119|Puerto Rican:Style:1|R&B / Soul:Music:199|Raíces:Music:2|Reggae / Dancehall:Music:2|Reggae:Music:3|Regional Mexicano:Music:3|Rhythm And Blues:Music:205|Riker:Other:3|Rock & Roll:Music:3|Rock Y Alternativo:Music:8|Rock:Music:320|Romantic:Other:4|Rumer:Other:2|Salsa Y Tropical:Music:58|Sasha:Other:5|Season 20:Other:17|Season 21:Other:25|Season 22:Other:9|Season 23:Other:8|Season 24:Other:10|Season 25:Other:9|Season 27:Other:4|Sertanejo:Music:3|Sharna:Other:15|Singer/Songwriter:Music:33|Slow:Tempo:48|Smooth Jazz:Music:2|Smooth:Tempo:1|Social:Style:39|Soft Rock:Music:3|Soul:Music:13|Soul:Other:2|Soundtrack:Music:191|Spanish:Other:32|Strict:Tempo:2|Strong:Tempo:1|Tamar:Other:6|Terrell:Other:2|Top 100:Other:29|Traditional:Music:6|Traditional:Style:52|Unconventional:Style:30|United States:Other:80|Upbeat:Other:2|Val:Other:13|Very Slow:Tempo:7|Vocal Jazz:Music:5|Vocal Pop:Music:12|Vocal:Music:45|Weak:Tempo:2|Wedding:Other:230|Week 2:Other:2|Week 3:Other:2|Witney:Other:5|World:Music:96|album-oriented-rock:Music:2|alt-country-rock:Music:4|baroque-pop:Music:11|brazilian-jazz:Music:5|broadway-and-vocal:Music:12|classic-rock:Music:7|dance-and-dj:Music:248|double-time:Tempo:9|half-time:Tempo:3|k-pop:Music:2",
    "danceLinks": [
      {
        "id": "30f4859b-e897-4d21-9395-e6212fd0274a",
        "danceId": "RMB",
        "description": "Wikipedia",
        "link": "http://en.wikipedia.org/wiki/Rumba_(dance)"
      }
    ],
    "seoName": "rumba",
    "danceType": {
      "id": "RMB",
      "name": "Rumba",
      "meter": {
        "numerator": 4,
        "denominator": 4
      },
      "tempoRange": {
        "min": 26.0,
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
              "level": "Bronze"
            },
            {
              "organization": "NDCA",
              "tempoRange": {
                "min": 32.0,
                "max": 36.0
              },
              "level": "Silver,Gold"
            },
            {
              "organization": "DanceSport",
              "tempoRange": {
                "min": 30.0,
                "max": 34.0
              }
            }
          ]
        },
        {
          "tempoRange": {
            "min": 26.0,
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
              "competitor": "ProAm"
            },
            {
              "organization": "NDCA",
              "tempoRange": {
                "min": 26.0,
                "max": 26.0
              },
              "competitor": "Professional,Amateur"
            }
          ]
        }
      ]
    }
}
`;

const boleroString = `{
  "danceId": "BOL",
  "danceName": "Bolero",
  "songCount": 463,
  "songCountExplicit": 462,
  "songCountImplicit": 1,
  "maxWeight": 11,
  "seoName": "bolero",
  "danceType": {
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
            "level": "Silver,Gold"
          }
        ]
      }
    ],
    "groupName": "Latin"
  }
}
`;

const chaString = `{
  "danceId": "CHA",
  "danceName": "Cha Cha",
  "blogTag": "cha-cha",
  "songCount": 7211,
  "songCountExplicit": 7211,
  "maxWeight": 38,
  "danceLinks": [],
  "seoName": "cha-cha",
  "danceType": {
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
        "exceptions": []
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
            }
          }
        ]
      }
    ],
    "groupName": "Latin",
    "blogTag": "cha-cha"
  }
}`;

function loadDance(data: string): DanceStats | undefined {
  const serializer = new TypedJSON(DanceStats);
  const dance = serializer.parse(data);

  return dance;
}

function loadRumba(): DanceStats | undefined {
  return loadDance(rumbaString);
}

function loadBolero(): DanceStats | undefined {
  return loadDance(boleroString);
}

function loadCha(): DanceStats | undefined {
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
    try {
      const rumba = JSON.parse(rumbaString);

      expect(rumba).toBeDefined();
      expect(rumba!.danceId).toEqual("RMB");
    } catch (e) {
      expect(e.name).toBeUndefined();
    }
  });

  it("should load a dancestat object", () => {
    const rumba = loadRumba();

    expect(rumba).toBeDefined();
    expect(rumba).toBeInstanceOf(DanceStats);
    expect(rumba!.danceId).toEqual("RMB");
  });

  it("should filter tempo by style", () => {
    const rumba = loadRumba()!.danceType;
    const americanRange = rumba!.filteredTempo(["american-rhythm"], []);

    expect(americanRange).toBeDefined();
    expect(americanRange!.min).toEqual(30);
    expect(americanRange!.max).toEqual(36);

    const internationalRange = rumba!.filteredTempo(
      ["international-latin"],
      []
    );
    expect(internationalRange).toBeDefined();
    expect(internationalRange!.min).toEqual(26);
    expect(internationalRange!.max).toEqual(27);

    const all = rumba!.filteredTempo([], []);
    expect(all).toBeDefined();
    expect(all!.min).toEqual(26);
    expect(all!.max).toEqual(36);

    const several = rumba!.filteredTempo(
      ["american-rhythm", "international-latin", "social"],
      []
    );
    expect(several).toBeDefined();
    expect(several!.min).toEqual(26);
    expect(several!.max).toEqual(36);
  });

  it("should filter tempo by organization (rumba)", () => {
    const rumba = loadRumba()!.danceType;

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
    expect(internationalDanceSport!.min).toEqual(26);
    expect(internationalDanceSport!.max).toEqual(27);

    const danceSport = rumba!.filteredTempo([], ["dancesport"]);
    expect(danceSport).toBeDefined();
    expect(danceSport!.min).toEqual(26);
    expect(danceSport!.max).toEqual(34);
  });

  it("should filter tempo by organization (bolero)", () => {
    const bolero = loadBolero()!.danceType;

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
    const cha = loadCha()!.danceType;

    const none = cha!.filteredTempo([], []);
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
