import { describe, it, expect } from "vitest";
import { Song } from "../Song";
import { SongHistory } from "../SongHistory";
import { PropertyType, SongProperty } from "../SongProperty";
import { AmazonPurchaseInfo, ServiceObjectType, ServiceType } from "../Purchase";
import { DanceRating } from "../DanceRating";

describe("Song", () => {
  describe("tempoForDance", () => {
    it("returns per-dance tempo when an override exists", () => {
      const song = new Song({
        tempo: 171,
        danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1, tempo: 214 })],
      });

      expect(song.tempoForDance("VWZ")).toBe(214);
    });

    it("falls back to song tempo when dance override is missing", () => {
      const song = new Song({
        tempo: 171,
        danceRatings: [new DanceRating({ danceId: "VWZ", weight: 1 })],
      });

      expect(song.tempoForDance("VWZ")).toBe(171);
      expect(song.tempoForDance("CHA")).toBe(171);
      expect(song.tempoForDance()).toBe(171);
    });
  });

  describe("isUserModified", () => {
    it("should return true when field is modified by real user", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "RealUser" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });

      const song = Song.fromHistory(history);

      expect(song.isUserModified(PropertyType.tempoField)).toBe(true);
      expect(song.isUserModified(PropertyType.titleField)).toBe(true);
      expect(song.isUserModified(PropertyType.artistField)).toBe(true);
    });

    it("should return false when field is modified by bot", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User:Proxy", value: "TempoBot|P" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });

      const song = Song.fromHistory(history);

      expect(song.isUserModified(PropertyType.tempoField)).toBe(false);
      expect(song.isUserModified(PropertyType.titleField)).toBe(false);
      expect(song.isUserModified(PropertyType.artistField)).toBe(false);
    });

    it("should return false when field is not set", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "RealUser" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });

      const song = Song.fromHistory(history);

      expect(song.isUserModified(PropertyType.tempoField)).toBe(false);
      expect(song.isUserModified(PropertyType.lengthField)).toBe(false);
    });

    it("should track user override of bot value", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User:Proxy", value: "TempoBot|P" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "RealUser" }),
          new SongProperty({ name: "Tempo", value: "125" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });

      const song = Song.fromHistory(history);

      expect(song.isUserModified(PropertyType.titleField)).toBe(false); // Bot set title
      expect(song.isUserModified(PropertyType.tempoField)).toBe(true); // User overrode tempo
    });
  });

  describe("propLastSetBy", () => {
    it("returns the username of the human who set the field", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });
      const song = Song.fromHistory(history);
      expect(song.propLastSetBy(PropertyType.tempoField)).toBe("alice");
    });

    it("returns undefined when field was only set by a bot", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User:Proxy", value: "TempoBot|P" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });
      const song = Song.fromHistory(history);
      expect(song.propLastSetBy(PropertyType.tempoField)).toBeUndefined();
    });

    it("returns undefined when field has never been set", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });
      const song = Song.fromHistory(history);
      expect(song.propLastSetBy(PropertyType.tempoField)).toBeUndefined();
    });

    it("updates to the latest human editor when multiple users edit the field", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "bob" }),
          new SongProperty({ name: "Tempo", value: "130" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });
      const song = Song.fromHistory(history);
      expect(song.propLastSetBy(PropertyType.tempoField)).toBe("bob");
    });

    it("ignores a subsequent bot edit — last human editor is retained", () => {
      const history = new SongHistory({
        id: "test123",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Tempo", value: "120" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User:Proxy", value: "TempoBot|P" }),
          new SongProperty({ name: "Tempo", value: "125" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
        ],
      });
      const song = Song.fromHistory(history);
      // Bot edit is blocked from overwriting the human value, so alice remains last setter
      expect(song.propLastSetBy(PropertyType.tempoField)).toBe("alice");
    });
  });

  describe("dance rating per-user cap", () => {
    function makeHistory(properties: SongProperty[]): SongHistory {
      return new SongHistory({ id: "test", properties });
    }

    it("caps a pseudo user at +1 for the same dance (two votes)", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "ArthurMurrays|P" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "ArthurMurrays|P" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(1);
    });

    it("caps a pseudo user at +1 for the same dance (three votes)", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "Studio|P" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "Studio|P" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "Studio|P" }),
          new SongProperty({ name: "Time", value: "2024-03-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(1);
    });

    it("caps a real user at +1 for the same dance", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(1);
    });

    it("allows two different users to each contribute +1", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "bob" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(2);
    });

    it("removes the dance when upvote is followed by downvote", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA-1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")).toBeUndefined();
    });

    it("allows up-down-up sequence resulting in net +1", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA-1" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "alice" }),
          new SongProperty({ name: "Time", value: "2024-03-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(1);
    });

    it("does not cap batch user high-delta votes", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "batch" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "SFT+5" }),
          new SongProperty({ name: "Tag+", value: "Slow Foxtrot:Dance" }),
        ]),
      );
      expect(song.findDanceRatingById("SFT")?.weight).toBe(5);
    });

    it("applies cap per dance independently", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "Studio|P" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "DanceRating", value: "SLS+1" }),
          new SongProperty({ name: "Tag+", value: "Cha Cha:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "Studio|P" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "CHA+1" }),
          new SongProperty({ name: "DanceRating", value: "SLS+1" }),
        ]),
      );
      expect(song.findDanceRatingById("CHA")?.weight).toBe(1);
      expect(song.findDanceRatingById("SLS")?.weight).toBe(1);
    });

    it("does not cap tempo-bot high-delta votes", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "tempo-bot" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "FXT+5" }),
          new SongProperty({ name: "Tag+", value: "Slow Foxtrot:Dance" }),
        ]),
      );
      expect(song.findDanceRatingById("FXT")?.weight).toBe(5);
    });

    it("does not cap tempo-bot repeated votes on the same dance", () => {
      const song = Song.fromHistory(
        makeHistory([
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "tempo-bot" }),
          new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          new SongProperty({ name: "DanceRating", value: "FXT+3" }),
          new SongProperty({ name: "Tag+", value: "Slow Foxtrot:Dance" }),
          new SongProperty({ name: ".Edit", value: "" }),
          new SongProperty({ name: "User", value: "tempo-bot" }),
          new SongProperty({ name: "Time", value: "2024-02-01T00:00:00.000Z" }),
          new SongProperty({ name: "DanceRating", value: "FXT+2" }),
        ]),
      );
      expect(song.findDanceRatingById("FXT")?.weight).toBe(5);
    });
  });

  describe("getPurchaseInfos", () => {
    function makeSong(title: string, artist: string): Song {
      return Song.fromHistory(
        new SongHistory({
          id: "test-id",
          properties: [
            new SongProperty({ name: ".Create", value: "" }),
            new SongProperty({ name: "User", value: "alice" }),
            new SongProperty({ name: "Time", value: "2024-01-01T00:00:00.000Z" }),
            new SongProperty({ name: "Title", value: title }),
            new SongProperty({ name: "Artist", value: artist }),
          ],
        }),
      );
    }

    it("always includes Amazon even when no ASIN is stored", () => {
      const song = makeSong("Test Song", "Test Artist");
      const purchases = song.getPurchaseInfos();
      const amazon = purchases.find((p) => p.service === ServiceType.Amazon);
      expect(amazon).toBeDefined();
    });

    it("Amazon link uses title and artist as search query", () => {
      const song = makeSong("Smooth Criminal", "Michael Jackson");
      const purchases = song.getPurchaseInfos();
      const amazon = purchases.find((p) => p.service === ServiceType.Amazon) as AmazonPurchaseInfo;
      expect(amazon).toBeDefined();
      expect(amazon.link).toContain("amazon.com/s");
      expect(amazon.link).toContain(encodeURIComponent("Michael Jackson Smooth Criminal"));
      expect(amazon.link).toContain("tag=msc4dnc-20");
    });

    it("Amazon link includes only the search path (not an ASIN product link)", () => {
      const song = makeSong("Any Song", "Any Artist");
      const purchases = song.getPurchaseInfos();
      const amazon = purchases.find((p) => p.service === ServiceType.Amazon) as AmazonPurchaseInfo;
      expect(amazon.link).not.toContain("/gp/product/");
    });

    it("does not include ITunes or Spotify when no IDs are stored", () => {
      const song = makeSong("Test Song", "Test Artist");
      const purchases = song.getPurchaseInfos();
      expect(purchases.find((p) => p.service === ServiceType.ITunes)).toBeUndefined();
      expect(purchases.find((p) => p.service === ServiceType.Spotify)).toBeUndefined();
    });
  });

  describe("AmazonPurchaseInfo.link fallback (PurchaseEncoded.decode() path)", () => {
    it("falls back to ASIN direct link when artist and songTitle are not set but songId is present", () => {
      const info = new AmazonPurchaseInfo({ songId: "D:B00ABCDE12" });
      expect(info.link).toBe("https://www.amazon.com/dp/B00ABCDE12?tag=msc4dnc-20");
    });

    it("falls back to generic Amazon music search when neither title/artist nor songId is present", () => {
      const info = new AmazonPurchaseInfo({});
      expect(info.link).toBe("https://www.amazon.com/s?i=digital-music&tag=msc4dnc-20");
    });

    it("prefers title+artist search over ASIN when both are available", () => {
      const info = new AmazonPurchaseInfo({ songId: "D:B00ABCDE12" });
      info.artist = "The Beatles";
      info.songTitle = "Let It Be";
      expect(info.link).toContain("amazon.com/s");
      expect(info.link).toContain(encodeURIComponent("The Beatles Let It Be"));
      expect(info.link).not.toContain("/dp/");
    });
  });

  describe("buildAlbumInfo (via fromHistory)", () => {
    function makeHistory(properties: SongProperty[]): SongHistory {
      return new SongHistory({
        id: "test-id",
        properties: [
          new SongProperty({ name: ".Create", value: "" }),
          new SongProperty({ name: "User", value: "batch-s" }),
          new SongProperty({ name: "Time", value: new Date().toISOString() }),
          new SongProperty({ name: "Title", value: "Test Song" }),
          new SongProperty({ name: "Artist", value: "Test Artist" }),
          ...properties,
        ],
      });
    }

    it("accumulates two Purchase properties for the same slot into a single album", () => {
      // Two edit blocks each adding a different Spotify id for the same album index —
      // both should end up on the AlbumDetails without the first being overwritten.
      const history = makeHistory([
        new SongProperty({ name: "Album:00", value: "My Album" }),
        new SongProperty({ name: "Track:00", value: "3" }),
        new SongProperty({ name: "Purchase:00:SS", value: "id1" }),
        new SongProperty({ name: "Purchase:00:SS", value: "id2" }),
      ]);

      const song = Song.fromHistory(history);

      expect(song.albums).toHaveLength(1);
      // Primary id (first-added) is returned by getId
      expect(song.albums![0].purchase.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe(
        "id1",
      );
    });

    it("Purchase- removes a specific id while leaving siblings intact", () => {
      const history = makeHistory([
        new SongProperty({ name: "Album:00", value: "My Album" }),
        new SongProperty({ name: "Purchase:00:SS", value: "id1" }),
        new SongProperty({ name: "Purchase:00:SS", value: "id2" }),
        new SongProperty({ name: "Purchase-:00:SS", value: "id1" }),
      ]);

      const song = Song.fromHistory(history);

      // id1 was removed; id2 becomes the new primary
      expect(song.albums![0].purchase.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe(
        "id2",
      );
    });

    it("Purchase- removing the last id clears the slot", () => {
      const history = makeHistory([
        new SongProperty({ name: "Album:00", value: "My Album" }),
        new SongProperty({ name: "Purchase:00:SS", value: "id1" }),
        new SongProperty({ name: "Purchase-:00:SS", value: "id1" }),
      ]);

      const song = Song.fromHistory(history);

      expect(
        song.albums![0].purchase.getId(ServiceType.Spotify, ServiceObjectType.Song),
      ).toBeUndefined();
    });
  });
});
