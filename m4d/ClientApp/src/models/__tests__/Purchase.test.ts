import { describe, it, expect } from "vitest";
import { PurchaseEncoded, ServiceObjectType, ServiceType } from "../Purchase";

describe("PurchaseEncoded", () => {
  describe("addId / getId", () => {
    it("returns the id when a single id is stored", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "abc123");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
    });

    it("accumulates a second id without losing the first", () => {
      // Spotify periodically reissues different ids for the same recording — both
      // should be tracked so all ids remain searchable.
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "abc123");
      encoded.addId("ss", "def456");

      // Primary (first-added) id is used for links
      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
    });

    it("ignores duplicate addId calls", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "abc123");
      encoded.addId("ss", "abc123");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
    });

    it("returns undefined when nothing is stored", () => {
      const encoded = new PurchaseEncoded();
      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBeUndefined();
    });

    it("tracks different service types independently", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "spotifySongId");
      encoded.addId("sa", "spotifyAlbumId");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("spotifySongId");
      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Album)).toBe("spotifyAlbumId");
    });

    it("throws on an invalid type length", () => {
      const encoded = new PurchaseEncoded();
      expect(() => encoded.addId("sss", "id")).toThrow();
    });
  });

  describe("removeId", () => {
    it("removes a specific id leaving the sibling as the new primary", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "id1");
      encoded.addId("ss", "id2");

      encoded.removeId("ss", "id1");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("id2");
    });

    it("removing the only id leaves the slot empty", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "abc123");

      encoded.removeId("ss", "abc123");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBeUndefined();
    });

    it("is a no-op when the id is not present", () => {
      const encoded = new PurchaseEncoded();
      encoded.addId("ss", "abc123");

      encoded.removeId("ss", "unknownId");

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
    });

    it("is a no-op when the slot is empty", () => {
      const encoded = new PurchaseEncoded();
      expect(() => encoded.removeId("ss", "abc123")).not.toThrow();
    });
  });

  describe("JSON API path (getId via @jsonMember properties)", () => {
    it("falls back to the deserialized property when no addId calls were made", () => {
      // Simulates the JSON API path where typedjson sets the property directly
      // and addId is never called.
      const encoded = new PurchaseEncoded();
      encoded.ss = "jsonDeserializedId";

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("jsonDeserializedId");
    });

    it("strips a trailing bracketed annotation from the deserialized id", () => {
      const encoded = new PurchaseEncoded();
      encoded.ss = "abc123[annotation]";

      expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
    });
  });
});
