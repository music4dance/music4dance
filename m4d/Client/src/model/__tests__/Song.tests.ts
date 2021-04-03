import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { SongHistory } from "../SongHistory";
import { Song } from "../Song";
import { ServiceType, PurchaseInfo } from "../Purchase";

import lalalaHistory from "./data/lalala-history.json";
import lalalaSong from "./data/lalala-song.json";
import history from "./data/simple-history.json";

import songsJson from "./data/songs.json";
import historiesJson from "./data/histories.json";

const song = {
  songId: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
  tempo: 183.7,
  title: "Pick-A-Rib",
  artist: "Michael Gamble",
  length: 208,
  sample:
    "https://p.scdn.co/mp3-preview/1002783fec8075c0b3ca970a91381d7227971fb6?cid=***REMOVED***",
  danceability: 0.65,
  energy: 0.247,
  valence: 0.858,
  created: "2019-10-01T10:47:57",
  modified: "2019-10-01T10:48:18",
  tags: [
    {
      value: "4/4",
      category: "Tempo",
      count: 1,
    },
    {
      value: "East Coast Swing",
      category: "Dance",
      count: 1,
    },
    {
      value: "Jazz",
      category: "Music",
      count: 2,
    },
    {
      value: "Lindy Hop",
      category: "Dance",
      count: 1,
    },
  ],
  danceRatings: [
    {
      danceId: "ECS",
      weight: 2,
    },
    {
      danceId: "LHP",
      weight: 2,
    },
    {
      danceId: "SWG",
      weight: 2,
    },
  ],
  modifiedBy: [
    {
      userName: "MaggieHaggerty",
    },
    {
      userName: "batch-a",
    },
    {
      userName: "batch-i",
    },
    {
      userName: "batch-s",
    },
    {
      userName: "batch-e",
    },
  ],
  albums: [
    {
      name: "Michael Gamble & The Rhythm Serenaders",
      track: 5,
      purchase: {
        sa: "2Vk6xGoNXnY7YJlHmCWWNV",
        ss: "4MQ23wXDxF03T1FjwpHtq3[US]",
        as: "D:B01GQZDE8C",
        aa: "D:B01GQZD5SG",
        is: "1121446561",
        ia: "1121446386",
      },
    },
  ],
};

describe("song load from history tests", () => {
  it("should load a simple song", () => {
    const h = TypedJSON.parse(history, SongHistory);

    expect(h).toBeDefined();
    expect(h?.id).toEqual("ec118d17-5d3c-481a-9777-4fcdd087c0b1");

    const s = Song.fromHistory(h!);
    expect(s).toBeDefined();
    expect(s?.title).toEqual("Pick-A-Rib");
    expect(s?.artist).toEqual("Michael Gamble");
    expect(s?.length).toEqual(208);
    expect(s?.tempo).toEqual(183.7);
    expect(s?.valence).toEqual(0.858);
  });

  it("should load mutliple songs", () => {
    const histories = TypedJSON.parseAsArray(historiesJson, SongHistory);
    expect(histories).toBeDefined();

    const songs = TypedJSON.parseAsArray(songsJson, Song);
    expect(songs).toBeDefined();

    const songsFromHistory = histories.map((h) =>
      Song.fromHistory(h, "dwgray")
    );

    // TODO: If we want to get clean comparison
    //  Ordering of tags in C# appears to do something wierd with casing: it put DWTS before Dance Pop
    //  CurrentUserTags: C# side isn't setting count
    //  Album ordering not implemented in TS (and we don't want to, so turn it onf in C#)?
    songs.forEach((s) => {
      const sh = songsFromHistory.find((x) => x.songId === s.songId);
      expect(sh).toBeDefined();
      expect(sh).toEqual(s);
    });
  });
});

describe("song tests", () => {
  it("should load a simple song", () => {
    const s = TypedJSON.parse(song, Song);

    expect(s).toBeDefined();
    expect(s).toBeInstanceOf(Song);
    expect(s?.songId).toEqual("ec118d17-5d3c-481a-9777-4fcdd087c0b1");
  });

  it("should find purchase info", () => {
    const s = TypedJSON.parse(song, Song);

    const pi = s?.getPurchaseInfo(ServiceType.Spotify);
    expect(pi).toBeDefined();
    expect(pi).toBeInstanceOf(PurchaseInfo);
    expect(pi?.albumId).toEqual("2Vk6xGoNXnY7YJlHmCWWNV");
    expect(pi?.songId).toEqual("4MQ23wXDxF03T1FjwpHtq3");
  });

  it("should find all purchase info", () => {
    const s = TypedJSON.parse(song, Song);

    const pis = s?.getPurchaseInfos();
    expect(pis).toBeDefined();
    expect(pis).toBeInstanceOf(Array);
    expect(pis!.length).toEqual(3);
  });

  it("should load simple dance rating", () => {
    const history = {
      id: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
      properties: [
        {
          name: "DanceRating",
          value: "ATN+3",
        },
        {
          name: "DanceRating",
          value: "SFT+5",
        },
      ],
    };
    const h = TypedJSON.parse(history, SongHistory);
    const s = Song.fromHistory(h!);

    expect(s.danceRatings).toBeDefined();
    expect(s.danceRatings!.length).toEqual(2);
    const atn = s.danceRatings!.find((dr) => dr.id === "ATN");
    expect(atn).toBeDefined();
    expect(atn?.weight).toEqual(3);
  });

  it("should load complex dance rating", () => {
    const history = {
      id: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
      properties: [
        {
          name: "DanceRating",
          value: "ATN+3",
        },
        {
          name: "DanceRating",
          value: "SFT+5",
        },
        {
          name: "DanceRating",
          value: "ATN-2",
        },
        {
          name: "DanceRating",
          value: "ATN-1",
        },
        {
          name: "DanceRating",
          value: "SFT+2",
        },
      ],
    };
    const h = TypedJSON.parse(history, SongHistory);
    const s = Song.fromHistory(h!);

    expect(s.danceRatings).toBeDefined();
    expect(s.danceRatings!.length).toEqual(1);
    const atn = s.danceRatings!.find((dr) => dr.id === "ATN");
    expect(atn).toBeUndefined();
    const sft = s.danceRatings!.find((dr) => dr.id === "SFT");
    expect(sft).toBeDefined();
    expect(sft?.weight).toEqual(7);
  });

  it("should handle tags", () => {
    const history = {
      id: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
      properties: [
        {
          name: "Tag+",
          value: "dance-pop:Music",
        },
        {
          name: "Tag+",
          value: "40'S:Other",
        },
        {
          name: "Tag+",
          value: "Washington Indie:Music",
        },
        {
          name: "Tag-",
          value: "Dance Pop:Music",
        },
      ],
    };
    const h = TypedJSON.parse(history, SongHistory);
    const s = Song.fromHistory(h!);

    expect(s.tags).toBeDefined();
    expect(s.tags.length).toEqual(2);
    const dp = s.tags.find((t) => t.key === "Dance Pop:Music");
    expect(dp).toBeUndefined();
    const wi = s.tags.find((t) => t.key === "Washington Indie:Music");
    expect(wi).toBeDefined();
  });

  it("should handle user tags", () => {
    const history = {
      id: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
      properties: [
        {
          name: "User",
          value: "dwgray",
        },
        {
          name: "Tag+",
          value: "dance-pop:Music",
        },
        {
          name: "Tag+",
          value: "40'S:Other",
        },
        {
          name: "Tag+",
          value: "Washington Indie:Music",
        },
        {
          name: "Tag-",
          value: "Dance Pop:Music",
        },
        {
          name: "User",
          value: "m4d",
        },
        {
          name: "Tag+",
          value: "dance-pop:Music",
        },
        {
          name: "Tag+",
          value: "Washington Indie:Music",
        },
      ],
    };
    const h = TypedJSON.parse(history, SongHistory);
    const s = Song.fromHistory(h!, "dwgray");

    expect(s.tags).toBeDefined();
    expect(s.tags.length).toEqual(3);
    const dp = s.tags.find((t) => t.key === "Dance Pop:Music");
    expect(dp).toBeDefined();
    expect(dp?.count).toEqual(1);
    const wi = s.tags.find((t) => t.key === "Washington Indie:Music");
    expect(wi).toBeDefined();
    expect(wi?.count).toEqual(2);

    expect(s.currentUserTags).toBeDefined();
    expect(s.currentUserTags.length).toEqual(2);
    const dpu = s.currentUserTags.find((t) => t.key === "Dance Pop:Music");
    expect(dpu).toBeUndefined();
    const wiu = s.currentUserTags.find(
      (t) => t.key === "Washington Indie:Music"
    );
    expect(wiu).toBeDefined();
  });

  it("should handle dance tags", () => {
    const history = {
      id: "ec118d17-5d3c-481a-9777-4fcdd087c0b1",
      properties: [
        {
          name: "DanceRating",
          value: "ATN+3",
        },
        {
          name: "Tag+:ATN",
          value: "dance-pop:Music",
        },
        {
          name: "Tag+:ATN",
          value: "40'S:Other",
        },
        {
          name: "Tag+:ATN",
          value: "Washington Indie:Music",
        },
        {
          name: "Tag-:ATN",
          value: "Dance Pop:Music",
        },
      ],
    };
    const h = TypedJSON.parse(history, SongHistory);
    const s = Song.fromHistory(h!);

    const atn = s.danceRatings!.find((dr) => dr.danceId === "ATN");
    expect(atn).toBeDefined();
    expect(atn?.tags).toBeDefined();
    expect(atn?.tags.length).toEqual(2);
    const dp = atn?.tags.find((t) => t.key === "Dance Pop:Music");
    expect(dp).toBeUndefined();
    const wi = atn?.tags.find((t) => t.key === "Washington Indie:Music");
    expect(wi).toBeDefined();
  });

  it("should load full song history", () => {
    const h = TypedJSON.parse(lalalaHistory, SongHistory);
    const s = Song.fromHistory(h!);
    const b = TypedJSON.parse(lalalaSong, Song)!;

    expect(h).toBeDefined();
    expect(s).toBeDefined();

    expect(s).toEqual(b);
  });
});
