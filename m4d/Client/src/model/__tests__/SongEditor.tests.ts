import { DanceRatingVote, VoteDirection } from "@/DanceRatingDelta";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Song } from "../Song";
import { SongEditor } from "../SongEditor";
import { SongHistory } from "../SongHistory";
import { PropertyType } from "../SongProperty";
import history from "./data/simple-history.json";

describe("song history editor tests", () => {
  it("should create a simple song", () => {
    const e = new SongEditor("dwgray");

    expect(e).toBeDefined();
    expect(e).toBeInstanceOf(SongEditor);

    const empty = e.song;
    expect(empty).toBeDefined();
    expect(empty).toBeInstanceOf(Song);
    expect(empty.title).toBeFalsy();

    e.addProperty(PropertyType.titleField, "My Song");
    e.addProperty(PropertyType.artistField, "Myself");

    const simple = e.song;
    expect(simple).toBeDefined();
    expect(simple).toBeInstanceOf(Song);
    expect(simple.title).toEqual("My Song");
    expect(simple.artist).toEqual("Myself");
  });

  it("should add a simple property to a song", () => {
    const e = new SongEditor("dwgray", TypedJSON.parse(history, SongHistory)!);

    e.addProperty(PropertyType.titleField, "My Song");
    e.addProperty(PropertyType.artistField, "Myself");

    const s = e.song;
    expect(s).toBeDefined();
    expect(s).toBeInstanceOf(Song);
    expect(s.title).toEqual("My Song");
    expect(s.artist).toEqual("Myself");
    expect(s?.length).toEqual(208);
    expect(s?.tempo).toEqual(183.7);
    expect(s?.valence).toEqual(0.858);
  });

  it("should revert a song", () => {
    const e = new SongEditor("dwgray", TypedJSON.parse(history, SongHistory)!);

    e.addProperty(PropertyType.titleField, "My Song");
    e.addProperty(PropertyType.artistField, "Myself");
    e.revert();

    const simple = e.song;
    expect(simple).toBeDefined();
    expect(simple).toBeInstanceOf(Song);
    expect(simple.title).toEqual("Pick-A-Rib");
    expect(simple.artist).toEqual("Michael Gamble");
  });

  it("should add dance ratings to a song", () => {
    const e = new SongEditor("dwgray", TypedJSON.parse(history, SongHistory)!);

    e.danceVote(new DanceRatingVote("WCS", VoteDirection.Up));

    const s = e.song;
    expect(s.danceRatings?.length).toEqual(1);
    const wcs = s.danceRatings![0];
    expect(wcs.danceId).toEqual("WCS");
    expect(wcs.weight).toEqual(1);
    expect(
      s.tags.find(
        (t) => t.value === "West Coast Swing" && t.category === "Dance"
      )
    ).toBeTruthy();
  });

  it("should remove dance rating from a song", () => {
    const e = new SongEditor("dwgray", TypedJSON.parse(history, SongHistory)!);

    e.danceVote(new DanceRatingVote("WCS", VoteDirection.Up));
    e.danceVote(new DanceRatingVote("WCS", VoteDirection.Down));

    const s = e.song;
    expect(s.danceRatings?.length).toEqual(0);
    expect(
      s.tags.find(
        (t) => t.value === "West Coast Swing" && t.category === "Dance"
      )
    ).toBeFalsy();
  });

  it("should toggle liking a song", () => {
    const e = new SongEditor("dwgray", TypedJSON.parse(history, SongHistory)!);

    e.toggleLike();
    const songLike = e.song;
    expect(songLike.getUserModified("dwgray")?.like).toBeTruthy();

    e.toggleLike();
    const songHate = e.song;
    expect(songHate.getUserModified("dwgray")?.like).toEqual(false);

    e.toggleLike();
    const songUndefined = e.song;
    expect(songUndefined.getUserModified("dwgray")?.like).toBeUndefined();
  });
});
