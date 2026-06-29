import { describe, it, expect } from "vitest";
import { PurchaseEncoded, ServiceObjectType, ServiceType } from "../Purchase";

describe("PurchaseEncoded", () => {
  it("returns the id as-is when only one is stored", () => {
    const encoded = new PurchaseEncoded();
    encoded.addId("ss", "abc123");

    expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
  });

  it("uses only the primary (first) id when the slot holds more than one", () => {
    // Spotify (and other services) periodically reissue a different id for what is
    // otherwise the same recording - the server packs both into one value, separated
    // by ',' (see AlbumDetails.AddPurchaseId), so a link can only be built from the first.
    const encoded = new PurchaseEncoded();
    encoded.addId("ss", "abc123,def456");

    expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
  });

  it("still strips a trailing bracketed annotation after taking the primary id", () => {
    const encoded = new PurchaseEncoded();
    encoded.addId("ss", "abc123[note],def456");

    expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBe("abc123");
  });

  it("returns undefined when nothing is stored", () => {
    const encoded = new PurchaseEncoded();
    expect(encoded.getId(ServiceType.Spotify, ServiceObjectType.Song)).toBeUndefined();
  });
});
