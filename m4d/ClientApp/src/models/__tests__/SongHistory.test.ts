import { describe, it, expect, beforeEach } from "vitest";
import { SongHistory } from "../SongHistory";
import { SongProperty } from "../SongProperty";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Build a minimal SongHistory from a compact property array */
function makeHistory(props: { name: string; value: string }[]): SongHistory {
  return new SongHistory({
    id: "test-song-id",
    properties: props.map((p) => new SongProperty({ name: p.name, value: p.value })),
  });
}

/** A single create-by-human with a tag */
const humanCreate = (user = "EthanH|P", tagValue = "East Coast Swing:Dance") =>
  makeHistory([
    { name: ".Create", value: "" },
    { name: "User", value: user },
    { name: "Time", value: "3/17/2014 5:46:07 PM" },
    { name: "Tag+", value: tagValue },
    { name: "DanceRating", value: "ECS+1" },
  ]);

/** Catalog (batch|P) tag edit */
const catalogTag = (tag = "Pop:Music", time = "11/20/2014 11:30:37 AM") => [
  { name: ".Edit", value: "" },
  { name: "User", value: "batch|P" },
  { name: "Time", value: time },
  { name: "Tag+", value: tag },
];

/** Algorithmic service edit with a tag */
const algoTag = (user = "batch-s|P", tag = "Pop:Music", time = "12/18/2020 16:51:12") => [
  { name: ".Edit", value: "" },
  { name: "User", value: user },
  { name: "Time", value: time },
  { name: "Tag+", value: tag },
];

/** Algorithmic service edit with tempo */
const algoTempo = (user = "batch-e|P", tempo = "173.1", time = "02/09/2016 21:16:12") => [
  { name: ".Edit", value: "" },
  { name: "User", value: user },
  { name: "Time", value: time },
  { name: "Tempo", value: tempo },
];

/** Human edit with tempo */
const humanTempo = (user = "AdamT|P", tempo = "174.0", time = "4/16/2015 10:22:48 AM") => [
  { name: ".Edit", value: "" },
  { name: "User", value: user },
  { name: "Time", value: time },
  { name: "Tempo", value: tempo },
  { name: "Tag+", value: "Jive:Dance" },
];

/** Human edit with like */
const humanLike = (user = "dwgray", time = "2/06/2018 15:09:38") => [
  { name: ".Edit", value: "" },
  { name: "User", value: user },
  { name: "Time", value: time },
  { name: "Like", value: "True" },
];

// ---------------------------------------------------------------------------
// Full Candyman-like history fixture
// ---------------------------------------------------------------------------

const candymanProps = [
  { name: ".Create", value: "" },
  { name: "User", value: "EthanH|P" },
  { name: "Time", value: "3/17/2014 5:46:07 PM" },
  { name: "Tag+", value: "East Coast Swing:Dance|Lindy Hop:Dance" },
  { name: "DanceRating", value: "ECS+1" },
  { name: "DanceRating", value: "LHP+1" },
  // batch-a — only albums, no tracked fields
  { name: ".Edit", value: "" },
  { name: "User", value: "batch-a|P" },
  { name: "Time", value: "5/20/2014 3:36:22 PM" },
  { name: "Album:0", value: "Back To Basics" },
  // batch|P — Catalog — adds a tag
  ...catalogTag(),
  // batch-a — adds a music tag
  ...algoTag("batch-a|P", "Dance And Dj:Music", "12/10/2014 3:26:10 PM"),
  // batch-i — adds a music tag on same day → should merge with batch-a
  { name: ".Edit", value: "" },
  { name: "User", value: "batch-i|P" },
  { name: "Time", value: "12/10/2014 3:26:10 PM" },
  { name: "Tag+", value: "Dance:Music" },
  // AdamT — human — adds tempo + tags
  ...humanTempo(),
  // batch-e — algorithmic tempo
  ...algoTempo(),
  // JuliaS — human tag
  { name: ".Edit", value: "" },
  { name: "User", value: "JuliaS|P" },
  { name: "Time", value: "6/5/2014 8:46:10 PM" },
  { name: "Tag+", value: "East Coast Swing:Dance" },
  // Like from anonymous user
  ...humanLike("7c91b359-13a5-44e6-bc13-0f6be61b2e39", "12/28/2017 23:52:53"),
];

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("SongHistory", () => {
  describe("userChanges", () => {
    it("includes human (|P) users with tags", () => {
      const h = humanCreate();
      expect(h.userChanges).toHaveLength(1);
      expect(h.userChanges[0]?.user).toBe("EthanH|P");
    });

    it("excludes Catalog (batch|P) which has only a non-tracked property", () => {
      const h = makeHistory([
        { name: ".Create", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
        ...catalogTag(),
      ]);
      const users = h.userChanges.map((c) => c.user);
      expect(users).toContain("EthanH|P");
      expect(users).not.toContain("batch|P");
    });

    it("excludes Catalog (batch|P) even when it has a tracked tag", () => {
      const h = makeHistory([
        ...catalogTag(),
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      const users = h.userChanges.map((c) => c.user);
      expect(users).not.toContain("batch|P");
      expect(users).toContain("EthanH|P");
    });

    it("excludes all algorithmic service users (batch-s, batch-a, batch-i, batch-e, batch-x, tempo-bot)", () => {
      const algoUsers = ["batch-s|P", "batch-a|P", "batch-i|P", "batch-e|P", "batch-x|P"];
      for (const algoUser of algoUsers) {
        const h = makeHistory([
          ...algoTag(algoUser),
          { name: ".Edit", value: "" },
          { name: "User", value: "EthanH|P" },
          { name: "Time", value: "3/17/2014 5:46:07 PM" },
          { name: "Tag+", value: "East Coast Swing:Dance" },
        ]);
        const users = h.userChanges.map((c) => c.user);
        expect(users, `${algoUser} should be excluded`).not.toContain(algoUser);
      }
    });

    it("excludes entries with no tracked properties", () => {
      // batch-a edit with only album data — no Tag+/Tempo/Like/Comment
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Album:0", value: "Back To Basics" },
        { name: ".Edit", value: "" },
        { name: "User", value: "JuliaS|P" },
        { name: "Time", value: "6/5/2014 8:46:10 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      const users = h.userChanges.map((c) => c.user);
      expect(users).not.toContain("EthanH|P"); // no tracked props
      expect(users).toContain("JuliaS|P");
    });

    it("includes human tempo changes", () => {
      const h = makeHistory([
        { name: ".Create", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
        ...humanTempo("AdamT|P", "174.0", "4/16/2015 10:22:48 AM"),
      ]);
      const users = h.userChanges.map((c) => c.user);
      expect(users).toContain("AdamT|P");
    });

    it("includes Like changes from regular users", () => {
      const h = makeHistory([...humanLike("dwgray", "2/06/2018 15:09:38")]);
      expect(h.userChanges).toHaveLength(1);
      expect(h.userChanges[0]?.user).toBe("dwgray");
    });

    it("includes like changes from anonymous (GUID) users", () => {
      const h = makeHistory([
        ...humanLike("7c91b359-13a5-44e6-bc13-0f6be61b2e39", "12/28/2017 23:52:53"),
      ]);
      expect(h.userChanges).toHaveLength(1);
    });

    it("merges same-user same-day edits into one entry", () => {
      // AdamT edits twice on the same day
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:00:00 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 02:00:00 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      expect(h.userChanges).toHaveLength(1);
      expect(h.userChanges[0]?.properties).toHaveLength(2);
    });

    it("does not merge same-user edits on different days", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/15/2015 10:00:00 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:00:00 AM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      expect(h.userChanges).toHaveLength(2);
    });

    it("returns empty array for history with no human edits", () => {
      const h = makeHistory([...catalogTag(), ...algoTempo()]);
      expect(h.userChanges).toHaveLength(0);
    });

    it("includes pseudo users (non-batch |P suffix)", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:22:48 AM" },
        { name: "Tag+", value: "Jive:Dance" },
      ]);
      expect(h.userChanges).toHaveLength(1);
      expect(h.userChanges[0]?.user).toBe("AdamT|P");
    });

    it("includes full Candyman user set", () => {
      const h = makeHistory(candymanProps);
      const users = h.userChanges.map((c) => c.user);
      expect(users).toContain("EthanH|P");
      expect(users).toContain("AdamT|P");
      expect(users).toContain("JuliaS|P");
      // anon like
      expect(users).toContain("7c91b359-13a5-44e6-bc13-0f6be61b2e39");
      // batch/algo should be absent
      expect(users).not.toContain("batch|P");
      expect(users).not.toContain("batch-a|P");
      expect(users).not.toContain("batch-e|P");
      expect(users).not.toContain("batch-s|P");
    });
  });

  describe("inclusiveChanges", () => {
    it("includes all of userChanges", () => {
      const h = makeHistory(candymanProps);
      const userUsers = new Set(h.userChanges.map((c) => c.user));
      const inclUsers = new Set(h.inclusiveChanges.map((c) => c.user));
      userUsers.forEach((u) => expect(inclUsers.has(u)).toBe(true));
    });

    it("includes Catalog (batch|P) when it has a tracked tag", () => {
      const h = makeHistory([...catalogTag(), ...humanCreate().properties]);
      expect(h.inclusiveChanges.some((c) => c.user === "batch|P")).toBe(true);
    });

    it("includes algorithmic users with tag properties", () => {
      const h = makeHistory([...algoTag("batch-s|P"), ...algoTag("batch-e|P")]);
      const users = h.inclusiveChanges.map((c) => c.user);
      expect(users).toContain("batch-s|P");
      expect(users).toContain("batch-e|P");
    });

    it("includes algorithmic users with tempo properties", () => {
      const h = makeHistory([...algoTempo("batch-e|P")]);
      expect(h.inclusiveChanges.some((c) => c.user === "batch-e|P")).toBe(true);
    });

    it("still excludes entries with no tracked properties", () => {
      // batch-a with only album data
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "batch-a|P" },
        { name: "Time", value: "5/20/2014 3:36:22 PM" },
        { name: "Album:0", value: "Back To Basics" },
      ]);
      expect(h.inclusiveChanges).toHaveLength(0);
    });

    it("Candyman: batch|P appears in inclusiveChanges but NOT userChanges", () => {
      const h = makeHistory(candymanProps);
      expect(h.userChanges.some((c) => c.user === "batch|P")).toBe(false);
      expect(h.inclusiveChanges.some((c) => c.user === "batch|P")).toBe(true);
    });

    it("Candyman: batch-e|P appears in inclusiveChanges but NOT userChanges", () => {
      const h = makeHistory(candymanProps);
      expect(h.userChanges.some((c) => c.user === "batch-e|P")).toBe(false);
      expect(h.inclusiveChanges.some((c) => c.user === "batch-e|P")).toBe(true);
    });

    it("AdamT|P properties render correctly — Tempo not mixed with tag data", () => {
      const h = makeHistory(candymanProps);
      const adamChange = h.inclusiveChanges.find((c) => c.user === "AdamT|P");
      expect(adamChange).toBeDefined();
      const tempoProp = adamChange?.properties.find((p) => p.name === "Tempo");
      expect(tempoProp?.value).toBe("174.0");
    });

    it("user editing both Tempo and Tag+ in one block produces a single change with both properties", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:22:48 AM" },
        { name: "Tempo", value: "174.0" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: "Tag+:JIV", value: "Modern:Style" },
      ]);
      expect(h.userChanges).toHaveLength(1);
      const change = h.userChanges[0];
      const names = change?.properties.map((p) => p.name);
      expect(names).toContain("Tempo");
      expect(names).toContain("Tag+");
      expect(names).toContain("Tag+:JIV");
    });

    it("Candyman: batch|P has Pop:Music property in inclusiveChanges", () => {
      const h = makeHistory(candymanProps);
      const batchChange = h.inclusiveChanges.find((c) => c.user === "batch|P");
      expect(batchChange).toBeDefined();
      expect(
        batchChange?.properties.some((p) => p.name === "Tag+" && p.value === "Pop:Music"),
      ).toBe(true);
    });

    it("Candyman: batch-a|P has Dance And Dj:Music property in inclusiveChanges", () => {
      const h = makeHistory(candymanProps);
      const batchAChange = h.inclusiveChanges.find((c) => c.user === "batch-a|P");
      expect(batchAChange).toBeDefined();
      expect(batchAChange?.properties.some((p) => p.value === "Dance And Dj:Music")).toBe(true);
    });

    it("algo tempo tag (batch-e|P) has Tempo property in inclusiveChanges", () => {
      const h = makeHistory([...algoTempo("batch-e|P", "173.1")]);
      const change = h.inclusiveChanges.find((c) => c.user === "batch-e|P");
      expect(change).toBeDefined();
      expect(change?.properties.some((p) => p.name === "Tempo" && p.value === "173.1")).toBe(true);
    });

    it("algo music tag (batch-s|P) has Tag+ property in inclusiveChanges", () => {
      const h = makeHistory([...algoTag("batch-s|P", "Pop:Music")]);
      const change = h.inclusiveChanges.find((c) => c.user === "batch-s|P");
      expect(change).toBeDefined();
      expect(change?.properties.some((p) => p.name === "Tag+" && p.value === "Pop:Music")).toBe(
        true,
      );
    });
  });

  describe("systemTagKeys", () => {
    it("returns tag keys last set by a batch/pseudo user", () => {
      const h = makeHistory([...catalogTag("Pop:Music")]);
      expect(h.systemTagKeys.has("Pop:Music")).toBe(true);
    });

    it("removes a key when a human subsequently adds it", () => {
      const h = makeHistory([
        ...catalogTag("Pop:Music"),
        { name: ".Edit", value: "" },
        { name: "User", value: "dwgray" },
        { name: "Time", value: "3/1/2020 10:00:00 AM" },
        { name: "Tag+", value: "Pop:Music" },
      ]);
      expect(h.systemTagKeys.has("Pop:Music")).toBe(false);
    });

    it("re-adds a key when a system user edits it after a human", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "dwgray" },
        { name: "Time", value: "3/1/2020 10:00:00 AM" },
        { name: "Tag+", value: "Pop:Music" },
        ...catalogTag("Pop:Music", "4/1/2020 11:00:00 AM"),
      ]);
      expect(h.systemTagKeys.has("Pop:Music")).toBe(true);
    });

    it("ignores dance-qualified tags (Tag+:JIV)", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "batch|P" },
        { name: "Time", value: "1/1/2020 10:00:00 AM" },
        { name: "Tag+:JIV", value: "International:Style" },
      ]);
      expect(h.systemTagKeys.has("International:Style")).toBe(false);
    });

    it("handles removed tags — deleted keys are not returned", () => {
      const h = makeHistory([
        ...catalogTag("Pop:Music"),
        { name: ".Edit", value: "" },
        { name: "User", value: "batch|P" },
        { name: "Time", value: "4/1/2020 11:00:00 AM" },
        { name: "Tag-", value: "Pop:Music" },
      ]);
      expect(h.systemTagKeys.has("Pop:Music")).toBe(false);
    });

    it("handles multiple tags in one property", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "batch-s|P" },
        { name: "Time", value: "1/1/2020 10:00:00 AM" },
        { name: "Tag+", value: "Dance Pop:Music|Pop:Music|Post Teen Pop:Music" },
      ]);
      expect(h.systemTagKeys.has("Dance Pop:Music")).toBe(true);
      expect(h.systemTagKeys.has("Pop:Music")).toBe(true);
      expect(h.systemTagKeys.has("Post Teen Pop:Music")).toBe(true);
    });

    it("returns empty set when no system tags exist", () => {
      // Must use a direct site user (no |P suffix) — pseudo users are 'system' for systemTagKeys
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "dwgray" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      expect(h.systemTagKeys.size).toBe(0);
    });

    it("pseudo users (|P suffix) ARE treated as system for systemTagKeys", () => {
      // This is by design: |P users are proxies, not direct site users
      const h = humanCreate("EthanH|P");
      expect(h.systemTagKeys.has("East Coast Swing:Dance")).toBe(true);
    });
  });

  describe("singleUserChanges", () => {
    it("returns only changes for the given user", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:22:48 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "JuliaS|P" },
        { name: "Time", value: "6/5/2014 8:46:10 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      const changes = h.singleUserChanges("AdamT|P");
      expect(changes).toHaveLength(1);
      expect(changes[0]?.user).toBe("AdamT|P");
    });

    it("returns empty array for unknown user", () => {
      const h = humanCreate("EthanH|P");
      expect(h.singleUserChanges("nonexistent")).toHaveLength(0);
    });
  });

  describe("recentUserChange", () => {
    it("returns the last change for the user", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/15/2015 10:00:00 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "AdamT|P" },
        { name: "Time", value: "4/16/2015 10:00:00 AM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      const recent = h.recentUserChange("AdamT|P");
      expect(recent).toBeDefined();
      expect(recent?.properties[0]?.value).toBe("East Coast Swing:Dance");
    });

    it("returns undefined for unknown user", () => {
      const h = humanCreate();
      expect(h.recentUserChange("nobody")).toBeUndefined();
    });
  });

  describe("latestChange", () => {
    it("returns the last userChange", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "4/15/2015 10:00:00 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "JuliaS|P" },
        { name: "Time", value: "4/16/2015 10:00:00 AM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      expect(h.latestChange()?.user).toBe("JuliaS|P");
    });

    it("returns undefined for empty history", () => {
      const h = new SongHistory({ id: "x", properties: [] });
      expect(h.latestChange()).toBeUndefined();
    });

    it("ignores batch/algo entries", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "1/1/2015 10:00:00 AM" },
        { name: "Tag+", value: "Jive:Dance" },
        ...algoTempo("batch-e|P", "173.1", "2/1/2020 10:00:00 AM"),
      ]);
      // latestChange uses userChanges so batch-e should be excluded
      expect(h.latestChange()?.user).toBe("EthanH|P");
    });
  });

  describe("Deanonymize", () => {
    it("replaces a UUID user id with the real username", () => {
      const uuid = "7c91b359-13a5-44e6-bc13-0f6be61b2e39";
      const h = makeHistory([...humanLike(uuid, "12/28/2017 23:52:53")]);
      const deanon = h.Deanonymize("dwgray", uuid);
      const userProp = deanon.properties.find(
        (p) => p.name === "User" && p.value.includes("dwgray"),
      );
      expect(userProp).toBeDefined();
      expect(deanon.properties.find((p) => p.value.includes(uuid))).toBeUndefined();
    });

    it("leaves other users unchanged", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "3/17/2014 5:46:07 PM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      const deanon = h.Deanonymize("dwgray", "some-other-uuid");
      expect(deanon.properties.find((p) => p.value === "EthanH|P")).toBeDefined();
    });
  });

  describe("isSorted", () => {
    it("is sorted when changes are in chronological order", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "1/1/2014 10:00:00 AM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "JuliaS|P" },
        { name: "Time", value: "6/5/2014 8:46:10 PM" },
        { name: "Tag+", value: "Lindy Hop:Dance" },
      ]);
      expect(h.isSorted).toBe(true);
    });

    it("is not sorted when changes are out of order", () => {
      const h = makeHistory([
        { name: ".Edit", value: "" },
        { name: "User", value: "JuliaS|P" },
        { name: "Time", value: "6/5/2014 8:46:10 PM" },
        { name: "Tag+", value: "Lindy Hop:Dance" },
        { name: ".Edit", value: "" },
        { name: "User", value: "EthanH|P" },
        { name: "Time", value: "1/1/2014 10:00:00 AM" },
        { name: "Tag+", value: "East Coast Swing:Dance" },
      ]);
      expect(h.isSorted).toBe(false);
    });
  });

  // ---------------------------------------------------------------------------
  // Full Candyman history fixture (from architecture/candyman.m4d)
  // Includes every block that has at least one tracked property, plus representative
  // blocks without them. The order matches the .m4d file exactly.
  // ---------------------------------------------------------------------------
  describe("Full Candyman history", () => {
    // Build the full fixture from candyman.m4d
    const candymanFullProps = [
      // .Create — EthanH|P
      { name: ".Create", value: "" },
      { name: "User", value: "EthanH|P" },
      { name: "Time", value: "3/17/2014 5:46:07 PM" },
      { name: "DanceRating", value: "ECS+1" },
      { name: "DanceRating", value: "LHP+1" },
      { name: "Tag+", value: "East Coast Swing:Dance|Lindy Hop:Dance" },
      // batch-a|P (5/20/2014) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-a|P" },
      { name: "Time", value: "5/20/2014 3:36:22 PM" },
      { name: "Album:0", value: "Back To Basics" },
      // batch|P (5/21/2014) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch|P" },
      { name: "Time", value: "5/21/2014 2:06:04 PM" },
      { name: "Track:0", value: "16" },
      // batch-i|P (5/21/2014) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-i|P" },
      { name: "Time", value: "5/21/2014 7:18:02 PM" },
      { name: "Album:0", value: "Back to Basics" },
      // JuliaS|P
      { name: ".Edit", value: "" },
      { name: "User", value: "JuliaS|P" },
      { name: "Time", value: "6/5/2014 8:46:10 PM" },
      { name: "DanceRating", value: "ECS+1" },
      { name: "Tag+", value: "East Coast Swing:Dance" },
      // LincolnA|P
      { name: ".Edit", value: "" },
      { name: "User", value: "LincolnA|P" },
      { name: "Time", value: "6/23/2014 1:56:23 PM" },
      { name: "DanceRating", value: "LHP+1" },
      { name: "Tag+", value: "Lindy Hop:Dance" },
      // batch|P (11/20/2014) — Catalog with tracked tag ← CATALOG
      { name: ".Edit", value: "" },
      { name: "User", value: "batch|P" },
      { name: "Time", value: "11/20/2014 11:30:37 AM" },
      { name: "Tag+", value: "Pop:Music" },
      // batch-a|P (12/10/2014) — algo with tracked tag
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-a|P" },
      { name: "Time", value: "12/10/2014 3:26:10 PM" },
      { name: "Album:00", value: "Back To Basics" },
      { name: "Tag+", value: "Dance And Dj:Music" },
      // batch-i|P (12/10/2014) — algo with tracked tag
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-i|P" },
      { name: "Time", value: "12/10/2014 3:26:10 PM" },
      { name: "Album:00", value: "Back to Basics" },
      { name: "Tag+", value: "Dance:Music" },
      // batch-x|P (12/10/2014) — algo with tracked tag
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-x|P" },
      { name: "Time", value: "12/10/2014 3:26:11 PM" },
      { name: "Tag+", value: "Electronic / Dance:Music|Pop:Music" },
      // AdamT|P — human with Tempo + Tag+ + dance-specific Tag+:JIV
      { name: ".Edit", value: "" },
      { name: "User", value: "AdamT|P" },
      { name: "Time", value: "4/16/2015 10:22:48 AM" },
      { name: "Tempo", value: "174.0" },
      { name: "Tag+", value: "Jive:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      { name: "Tag+:JIV", value: "Modern:Style" },
      // batch-s|P (1/14/2016) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-s|P" },
      { name: "Time", value: "01/14/2016 22:04:36" },
      { name: "Purchase:04:SS", value: "7zj5ZTermM0LKglr0Gj1z0" },
      // batch-s|P (2/8/2016) — Sample only (not tracked)
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-s|P" },
      { name: "Time", value: "02/08/2016 19:56:27" },
      { name: "Sample", value: "https://p.scdn.co/mp3-preview/fa2e5fcf" },
      // batch-e|P (2/9/2016) — algo Tempo + Tag+ ← KEY ALGORITHMIC ENTRY
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-e|P" },
      { name: "Time", value: "02/09/2016 21:16:12" },
      { name: "Tempo", value: "173.1" },
      { name: "Tag+", value: "4/4:Tempo" },
      // BonnieL|P
      { name: ".Edit", value: "" },
      { name: "User", value: "BonnieL|P" },
      { name: "Time", value: "08/25/2016 17:04:34" },
      { name: "Tag+", value: "Jive:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      // rhettbot|P
      { name: ".Edit", value: "" },
      { name: "User", value: "rhettbot|P" },
      { name: "Time", value: "04/17/2017 20:22:17" },
      { name: "Tag+", value: "East Coast Swing:Dance|Jive:Dance|Lindy Hop:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      { name: "DanceRating", value: "LHP+1" },
      { name: "DanceRating", value: "ECS+1" },
      // BrittanyFalconer|P — Tag+:JIV=International:Style
      { name: ".Edit", value: "" },
      { name: "User", value: "BrittanyFalconer|P" },
      { name: "Time", value: "09/04/2017 15:34:14" },
      { name: "Tag+", value: "Jive:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      { name: "Tag+:JIV", value: "International:Style" },
      // anonymous like (7c91b359...)
      { name: ".Edit", value: "" },
      { name: "User", value: "7c91b359-13a5-44e6-bc13-0f6be61b2e39" },
      { name: "Time", value: "12/28/2017 23:52:53" },
      { name: "Like", value: "True" },
      // anonymous user (2ef1ad1f...) — two edits same day → merge
      { name: ".Edit", value: "" },
      { name: "User", value: "2ef1ad1f-f5df-493b-8e35-c6a0636c0588" },
      { name: "Time", value: "02/06/2018 15:09:38" },
      { name: "Like", value: "True" },
      { name: ".Edit", value: "" },
      { name: "User", value: "2ef1ad1f-f5df-493b-8e35-c6a0636c0588" },
      { name: "Time", value: "02/06/2018 15:10:00" },
      { name: "Tag+", value: "Jive:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      // MaggieHaggerty|P — Tag+:ECS + Tag+:LHP style tags
      { name: ".Edit", value: "" },
      { name: "User", value: "MaggieHaggerty|P" },
      { name: "Time", value: "09/27/2018 03:21:43" },
      { name: "Tag+", value: "East Coast Swing:Dance|Lindy Hop:Dance" },
      { name: "DanceRating", value: "ECS+1" },
      { name: "DanceRating", value: "LHP+1" },
      { name: "Tag+:ECS", value: "Modern:Style" },
      { name: "Tag+:LHP", value: "Modern:Style" },
      // batch-a|P (9/27/2018) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-a|P" },
      { name: "Time", value: "09/27/2018 03:21:50" },
      { name: "Purchase:04:AS", value: "D:B0022VWIMI" },
      // batch-i|P (9/27/2018) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-i|P" },
      { name: "Time", value: "09/27/2018 03:21:50" },
      { name: "Album:05", value: "Candyman (Dance Vault Mixes) - EP" },
      // batch-s|P (9/27/2018) — no tracked props
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-s|P" },
      { name: "Time", value: "09/27/2018 03:21:50" },
      { name: "Purchase:04:SS", value: "7zj5ZTermM0LKglr0Gj1z0" },
      // batch-e|P (9/27/2018) — Tempo only (no Tag+)
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-e|P" },
      { name: "Time", value: "09/27/2018 03:21:50" },
      { name: "Tempo", value: "173.0" },
      // batch-s|P (9/27/2018) second entry — Sample only
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-s|P" },
      { name: "Time", value: "09/27/2018 03:21:50" },
      { name: "Sample", value: "https://p.scdn.co/mp3-preview/2e5d942ba57f" },
      // dwts|P — Tag+:JAZ=Alan:Other|Alexis:Other (dance-qualified other tags)
      { name: ".Edit", value: "" },
      { name: "User", value: "dwts|P" },
      { name: "Time", value: "11/03/2018 23:22:17" },
      {
        name: "Tag+",
        value: "DWTS:Other|Halloween:Other|Jazz:Dance|Season 27:Other|United States:Other",
      },
      { name: "DanceRating", value: "JAZ+1" },
      { name: "Tag+:JAZ", value: "Alan:Other|Alexis:Other" },
      // StephanieLienPham|P
      { name: ".Edit", value: "" },
      { name: "User", value: "StephanieLienPham|P" },
      { name: "Time", value: "12/18/2020 16:49:30" },
      { name: "Tag+", value: "Jive:Dance" },
      { name: "DanceRating", value: "JIV+1" },
      // batch-s|P (12/18/2020) — large music tag set ← KEY ALGORITHMIC ENTRY
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-s|P" },
      { name: "Time", value: "12/18/2020 16:51:12" },
      {
        name: "Tag+",
        value:
          "Dance Pop:Music|Moroccan Pop:Music|Pop Dance:Music|Pop Rap:Music|Pop:Music|Post Teen Pop:Music",
      },
      // batch-e|P (12/18/2020) — Tempo only
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-e|P" },
      { name: "Time", value: "12/18/2020 16:51:12" },
      { name: "Tempo", value: "173.0" },
      // roblosapiens — negative Lindy Hop vote (Tag+=!Lindy Hop:Dance)
      { name: ".Edit", value: "" },
      { name: "User", value: "roblosapiens" },
      { name: "Time", value: "11/24/2021 02:02:56" },
      { name: "DanceRating", value: "LHP-1" },
      { name: "Tag+", value: "!Lindy Hop:Dance" },
      // ArthurMurrayBeaverton|P (1/11/2023) — Tag+
      { name: ".Edit", value: "" },
      { name: "User", value: "ArthurMurrayBeaverton|P" },
      { name: "Time", value: "01/11/2023 04:35:02" },
      { name: "Tag+", value: "Single Swing:Dance" },
      { name: "DanceRating", value: "SSW+1" },
      // ArthurMurrayBeaverton|P (6/15/2024) — Wedding:Other tag
      { name: ".Edit", value: "" },
      { name: "User", value: "ArthurMurrayBeaverton|P" },
      { name: "Time", value: "06/15/2024 09:12:40" },
      { name: "Tag+", value: "Wedding:Other" },
      // batch-i|P (8/5/2023) — algo with tracked tag ← KEY ALGORITHMIC ENTRY
      { name: ".Edit", value: "" },
      { name: "User", value: "batch-i|P" },
      { name: "Time", value: "08/05/2023 09:09:57" },
      { name: "Tag+", value: "Dance:Music" },
      // spotify|P (10/6/2024) — pseudo user with tags
      { name: ".Edit", value: "" },
      { name: "User", value: "spotify|P" },
      { name: "Time", value: "10/06/2024 18:15:10" },
      { name: "Tag+", value: "Halloween:Other|Holiday:Other" },
      // Arthur Murray Carmichael|P (11/5/2025) — re-adds Lindy Hop
      { name: ".Edit", value: "" },
      { name: "User", value: "Arthur Murray Carmichael|P" },
      { name: "Time", value: "11/05/2025 09:01:41" },
      { name: "Tag+", value: "Lindy Hop:Dance|Single Swing:Dance" },
      { name: "DanceRating", value: "SSW+1" },
      { name: "DanceRating", value: "LHP+1" },
      // annebaaner (2/25/2025) — negative Lindy Hop vote
      { name: ".Edit", value: "" },
      { name: "User", value: "annebaaner" },
      { name: "Time", value: "02/25/2025 09:02:56" },
      { name: "DanceRating", value: "LHP-1" },
      { name: "Tag+", value: "!Lindy Hop:Dance" },
      // Jazzup'n'Dance (4/14/2026) — negative Lindy Hop vote (after Carmichael re-adds)
      { name: ".Edit", value: "" },
      { name: "User", value: "Jazzup'n'Dance" },
      { name: "Time", value: "04/14/2026 03:24:36" },
      { name: "DanceRating", value: "LHP-1" },
      { name: "Tag+", value: "!Lindy Hop:Dance" },
    ];

    it("inclusiveChanges includes all Catalog (batch|P) entries with tracked tags", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      // batch|P at 11/20/2014 has Pop:Music
      const batchEntry = h.inclusiveChanges.find(
        (c) => c.user === "batch|P" && c.properties.some((p) => p.value === "Pop:Music"),
      );
      expect(batchEntry, "batch|P with Pop:Music should appear").toBeDefined();
    });

    it("inclusiveChanges includes all algorithmic entries with tracked tags (full list)", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const users = h.inclusiveChanges.map((c) => c.user);
      // Algorithmic users that have tracked properties in this history
      expect(users, "batch-a|P (Dance And Dj:Music)").toContain("batch-a|P");
      expect(users, "batch-i|P (Dance:Music)").toContain("batch-i|P");
      expect(users, "batch-x|P (Electronic/Dance:Music)").toContain("batch-x|P");
      expect(users, "batch-e|P (Tempo + 4/4:Tempo)").toContain("batch-e|P");
      expect(users, "batch-s|P (music genre tags)").toContain("batch-s|P");
    });

    it("userChanges excludes all batch/algo users in full Candyman history", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const users = h.userChanges.map((c) => c.user);
      expect(users).not.toContain("batch|P");
      expect(users).not.toContain("batch-a|P");
      expect(users).not.toContain("batch-i|P");
      expect(users).not.toContain("batch-x|P");
      expect(users).not.toContain("batch-e|P");
      expect(users).not.toContain("batch-s|P");
    });

    it("AdamT appears in inclusiveChanges with Tempo, Tag+, and Tag+:JIV properties", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const adamChange = h.inclusiveChanges.find((c) => c.user === "AdamT|P");
      expect(adamChange, "AdamT|P should be in inclusiveChanges").toBeDefined();
      const names = adamChange!.properties.map((p) => p.name);
      expect(names, "should have Tempo").toContain("Tempo");
      expect(names, "should have Tag+").toContain("Tag+");
      expect(names, "should have Tag+:JIV (Modern:Style)").toContain("Tag+:JIV");
    });

    it("batch-e|P appears twice in inclusiveChanges (2016 and 2018 are different dates)", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const batchEChanges = h.inclusiveChanges.filter((c) => c.user === "batch-e|P");
      // batch-e|P edited on 02/09/2016 (Tempo+Tag+), 09/27/2018 (Tempo), 12/18/2020 (Tempo)
      expect(batchEChanges.length, "batch-e|P should appear multiple times").toBeGreaterThanOrEqual(
        2,
      );
    });

    it("activeTags correctly tracks current song-level tag state", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const active = h.activeTags;
      // Tags added and never removed
      expect(active.has("Pop:Music"), "Pop:Music should be active").toBe(true);
      expect(active.has("Jive:Dance"), "Jive:Dance should be active").toBe(true);
      expect(active.has("4/4:Tempo"), "4/4:Tempo should be active").toBe(true);
      // Dance-specific style tags should NOT be in activeTags (they have danceQualifier)
      expect(active.has("Modern:Style"), "Modern:Style should NOT be in activeTags").toBe(false);
      expect(
        active.has("International:Style"),
        "International:Style should NOT be in activeTags",
      ).toBe(false);
    });

    it("anonymous user (2ef1ad1f...) same-day Like+Tag+ edits are merged", () => {
      const h = new SongHistory({
        id: "candyman",
        properties: candymanFullProps.map(
          (p) => new SongProperty({ name: p.name, value: p.value }),
        ),
      });
      const anonChanges = h.userChanges.filter(
        (c) => c.user === "2ef1ad1f-f5df-493b-8e35-c6a0636c0588",
      );
      expect(anonChanges, "both same-day edits should merge into one").toHaveLength(1);
      const propNames = anonChanges[0]!.properties.map((p) => p.name);
      expect(propNames).toContain("Like");
      expect(propNames).toContain("Tag+");
    });
  });

  describe("fromString", () => {
    it("parses tab-delimited properties", () => {
      const s = ".Create=\tUser=EthanH|P\tTime=3/17/2014 5:46:07 PM\tTag+=East Coast Swing:Dance";
      const h = SongHistory.fromString(s);
      expect(h.properties).toHaveLength(4);
      expect(h.properties[0]?.name).toBe(".Create");
      expect(h.properties[1]?.value).toBe("EthanH|P");
    });

    it("uses provided songId", () => {
      const s = ".Create=\tUser=EthanH|P\tTime=3/17/2014 5:46:07 PM";
      const h = SongHistory.fromString(s, "my-song-id");
      expect(h.id).toBe("my-song-id");
    });

    it("generates a songId when none is provided", () => {
      const s = ".Create=\tUser=EthanH|P\tTime=3/17/2014 5:46:07 PM";
      const h = SongHistory.fromString(s);
      expect(h.id).toBeTruthy();
      expect(h.id.length).toBe(36); // UUID format
    });
  });
});
