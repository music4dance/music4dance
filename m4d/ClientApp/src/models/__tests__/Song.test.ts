import { describe, it, expect } from "vitest";
import { Song } from "../Song";
import { SongHistory } from "../SongHistory";
import { PropertyType, SongProperty } from "../SongProperty";

describe("Song", () => {
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
});
