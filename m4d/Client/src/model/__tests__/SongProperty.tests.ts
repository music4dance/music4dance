import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { SongProperty } from "../SongProperty";
import { SongHistory } from "../SongHistory";

const simpleName = { name: "name" };
const trackName = { name: "name:00:ss" };
const highTrackName = { name: "name:30:ss" };

describe("song property name tests", () => {
  it("should parse simple base  name", () => {
    const p = TypedJSON.parse(simpleName, SongProperty);

    expect(p).toBeDefined();
    expect(p?.baseName).toEqual("name");
  });

  it("should parse complex base name", () => {
    const p = TypedJSON.parse(trackName, SongProperty);

    expect(p).toBeDefined();
    expect(p?.baseName).toEqual("name");
  });

  it("should parse an album index", () => {
    const p = TypedJSON.parse(trackName, SongProperty);

    expect(p?.index).toEqual(0);
  });

  it("should parse a high album index", () => {
    const p = TypedJSON.parse(highTrackName, SongProperty);

    expect(p?.index).toEqual(30);
  });

  it("should fail on a non-track name", () => {
    const p = TypedJSON.parse(simpleName, SongProperty);

    expect(() => {
      p?.index;
    }).toThrow(/Attempted to retrieve part.*/);
  });

  it("should fail on a non-number track index", () => {
    const p = TypedJSON.parse({ name: "name:t:ss" }, SongProperty);

    expect(() => {
      p?.index;
    }).toThrow(/Index must be a number.*/);
  });

  it("should fail on a non-number track index", () => {
    const p = TypedJSON.parse({ name: "name:-1:ss" }, SongProperty);

    expect(() => {
      p?.index;
    }).toThrow(/Index must be a postitive integer.*/);
  });

  it("should parse dance qualifier", () => {
    const p = TypedJSON.parse({ name: "Tag+:TGO" }, SongProperty);

    expect(p?.danceQualifier).toEqual("TGO");
  });

  it("should parse an action", () => {
    const p = TypedJSON.parse({ name: ".Create" }, SongProperty);

    expect(p?.isAction).toBeTruthy();
  });

  it("should parse an action", () => {
    const p = TypedJSON.parse(simpleName, SongProperty);

    expect(p?.isAction).toBeFalsy();
  });
});

describe("song property value tests", () => {
  it("should parse a string", () => {
    const p = TypedJSON.parse({ name: "Title", value: "test" }, SongProperty);

    expect(p?.valueTyped).toEqual("test");
  });

  it("should parse a float", () => {
    const p = TypedJSON.parse(
      { name: "Valence", value: "0.345" },
      SongProperty
    );

    expect(p?.valueTyped).toEqual(0.345);
  });

  it("should parse an int", () => {
    const p = TypedJSON.parse({ name: "Length", value: "230" }, SongProperty);

    expect(p?.valueTyped).toEqual(230);
  });

  it("should parse a date ", () => {
    const p = TypedJSON.parse(
      { name: "Time", value: "08/29/2017 20:32:01" },
      SongProperty
    );

    const expected = new Date(2017, 7, 29, 20, 32, 1);
    expect(p?.valueTyped).toEqual(expected);
  });

  it("should parse a boolean true", () => {
    const p = TypedJSON.parse({ name: "Like", value: "true" }, SongProperty);

    expect(p?.valueTyped).toEqual(true);
  });

  it("should parse a boolean false", () => {
    const p = TypedJSON.parse({ name: "Like", value: "false" }, SongProperty);

    expect(p?.valueTyped).toEqual(false);
  });

  it("should parse a boolean undefined", () => {
    const p = TypedJSON.parse({ name: "Like", value: "null" }, SongProperty);

    expect(p?.valueTyped).toBeUndefined();
  });

  describe("song property value tests", () => {
    it("should load song History", () => {
      const json =
        '{ "id": "0771e724-fdac-49f2-9a0e-e275c8eaf13b", "properties": [ { "name": ".Create", "value": "" }, { "name": "User", "value": "dgsnure" }, { "name": "Time", "value": "09/27/2019 10:13:42" }, { "name": "Title", "value": "C\'est La Vie" }, { "name": "Artist", "value": "Marlhy" }, { "name": "Length", "value": "184" }, { "name": "Album:00", "value": "C\'est La Vie" }, { "name": "Track:00", "value": "1" }, { "name": "Tag+", "value": "Cha Cha:Dance" }, { "name": "DanceRating", "value": "CHA+2" }, { "name": "Purchase:00:SA", "value": "3lw7yOZP8ospwDW5HYR9NE" }, { "name": "Purchase:00:SS", "value": "2EYexewQCvTo2EcIgM1Nyi" }, { "name": "DanceRating", "value": "LTN+1" }, { "name": ".Edit", "value": "" }, { "name": "User", "value": "batch-a" }, { "name": "Time", "value": "09/27/2019 10:13:58" }, { "name": "Purchase:00:AS", "value": "D:B07XZK62FC" }, { "name": "Purchase:00:AA", "value": "D:B07XZK91QF" }, { "name": "Tag+", "value": "dance-and-dj:Music" }, { "name": ".Edit", "value": "" }, { "name": "User", "value": "batch-i" }, { "name": "Time", "value": "09/27/2019 10:13:58" }, { "name": "Purchase:00:IS", "value": "1480315409" }, { "name": "Purchase:00:IA", "value": "1480315070" }, { "name": "Tag+", "value": "Pop:Music" }, { "name": ".Edit", "value": "" }, { "name": "User", "value": "batch-s" }, { "name": "Time", "value": "09/27/2019 10:13:58" }, { "name": "Purchase:00:SS", "value": "2EYexewQCvTo2EcIgM1Nyi[NZ,AU,JP,HK,MY,PH,SG,TW,ID,TH,VN,IN,AE,OM,BG,BH,CY,EE,FI,GR,IL,JO,KW,LB,LT,LV,PS,QA,RO,SA,TR,AD,AT,BE,CH,CZ,DE,DK,EG,ES,FR,HU,IT,LI,LU,MC,MT,NL,NO,PL,SE,SK,ZA,DZ,GB,IE,MA,PT,TN,IS,AR,BR,CL,UY,BO,CA,DO,PY,US,CO,EC,MX,PA,PE,CR,GT,HN,NI,SV]" }, { "name": ".Edit", "value": "" }, { "name": "User", "value": "batch-e" }, { "name": "Time", "value": "09/27/2019 10:13:58" }, { "name": "Tempo", "value": "144.0" }, { "name": "Danceability", "value": "0.703" }, { "name": "Energy", "value": "0.652" }, { "name": "Valence", "value": "0.785" }, { "name": "Tag+", "value": "4/4:Tempo" }, { "name": ".Edit", "value": "" }, { "name": "Time", "value": "09/27/2019 10:13:58" }, { "name": "Sample", "value": "https://p.scdn.co/mp3-preview/10700bdfbf8dcc514e84431843d2bf6210b7719c?cid=***REMOVED***" } ] }';
      const h = TypedJSON.parse(json, SongHistory);

      expect(h).toBeDefined();
      expect(h).toBeInstanceOf(SongHistory);
      expect(h?.id).toEqual("0771e724-fdac-49f2-9a0e-e275c8eaf13b");
    });
  });
});
