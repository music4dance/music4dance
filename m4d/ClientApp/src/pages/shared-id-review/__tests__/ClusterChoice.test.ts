import { describe, expect, test } from "vitest";
import { TypedJSON } from "typedjson";
import { ClusterChoice, groupNames, leaveOut, lookupIds } from "../ClusterChoice";
import { SharedIdReviewCluster } from "../SharedIdReviewModel";
import { featCluster, rejectedCluster, songA, songB, songC, songD, songE } from "./model";

function choiceFor(cluster: unknown): ClusterChoice {
  return new ClusterChoice(TypedJSON.parse(cluster, SharedIdReviewCluster)!);
}

describe("ClusterChoice", () => {
  describe("defaults", () => {
    test("a clean cluster starts as a merge of every song in A, named by the proposal", () => {
      const choice = choiceFor(featCluster);

      expect(choice.action).toBe("Merge");
      expect(choice.groupOf(songA)).toBe("A");
      expect(choice.groupOf(songB)).toBe("A");
      expect(choice.names).toEqual({ title: null, artist: null, artists: null });
      expect(choice.isSplit).toBe(false);
      expect(choice.isDirty).toBe(false);
    });

    test("a cluster with a rejected pair starts as Leave, named by its first song without a proposal", () => {
      const choice = choiceFor(rejectedCluster);

      expect(choice.action).toBe("Leave");
      expect(choice.names).toEqual({ title: songC, artist: songC, artists: songC });
      expect(choice.isDirty).toBe(false);
    });

    test("there's a group for each song", () => {
      expect(groupNames(3)).toEqual(["A", "B", "C"]);
      expect(choiceFor(featCluster).groupNames).toEqual(["A", "B"]);
    });
  });

  describe("groups", () => {
    test("putting a song in another group splits the cluster", () => {
      const choice = choiceFor(featCluster);

      choice.setGroup(songB, "B");

      expect(choice.isSplit).toBe(true);
      expect(choice.isDirty).toBe(true);
    });

    test("leaving a song out splits it too", () => {
      const choice = choiceFor(featCluster);

      choice.setGroup(songB, leaveOut);

      expect(choice.isSplit).toBe(true);
    });

    test("choosing a group makes a cluster that started as Leave a merge, so its names apply", () => {
      const choice = choiceFor(rejectedCluster);
      expect(choice.action).toBe("Leave");
      expect(choice.canName(songC)).toBe(false);

      choice.setGroup(songE, leaveOut);

      expect(choice.action).toBe("Merge");
      expect(choice.canName(songC)).toBe(true);
      expect(choice.canName(songE)).toBe(false);
    });

    test("leaving out the song a name came from puts the name back on its default", () => {
      const choice = choiceFor(featCluster);
      choice.pickName("title", songB);
      choice.pickName("artist", songA);

      choice.setGroup(songB, leaveOut);

      expect(choice.names.title).toBeNull();
      expect(choice.names.artist).toBe(songA);
    });
  });

  describe("names", () => {
    test("a song outside every group can't name the merge", () => {
      const choice = choiceFor(featCluster);
      choice.setGroup(songB, leaveOut);

      choice.pickName("title", songB);

      expect(choice.canName(songB)).toBe(false);
      expect(choice.names.title).toBeNull();
    });

    test("names only apply to a merge", () => {
      const choice = choiceFor(featCluster);
      choice.setAction("Reject");

      choice.pickName("title", songB);

      expect(choice.canName(songA)).toBe(false);
      expect(choice.names.title).toBeNull();
    });

    test("picking a song's name, then the proposal again, is back to the default", () => {
      const choice = choiceFor(featCluster);

      choice.pickName("title", songB);
      expect(choice.isDirty).toBe(true);
      choice.pickName("title", null);

      expect(choice.isDirty).toBe(false);
    });
  });

  describe("removals", () => {
    test("removing an album covers its ids, and its ids aren't sent on their own", () => {
      const choice = choiceFor(featCluster);
      choice.setIdRemoved(songA, 0, "S:sp1", true);

      choice.setAlbumRemoved(songA, 0, true);

      expect(choice.isIdRemoved(songA, 0, "S:sp1")).toBe(true);
      expect(choice.isIdRemoved(songA, 0, "R:USRC1")).toBe(true);
      expect(choice.removals).toEqual([{ songId: songA, album: 0 }]);
    });

    test("putting the album back leaves the id that was checked", () => {
      const choice = choiceFor(featCluster);
      choice.setIdRemoved(songA, 0, "S:sp1", true);
      choice.setAlbumRemoved(songA, 0, true);

      choice.setAlbumRemoved(songA, 0, false);

      expect(choice.removals).toEqual([{ songId: songA, album: 0, id: "S:sp1" }]);
    });

    test("removals use the album's index, not its position", () => {
      const choice = choiceFor(featCluster);

      choice.setAlbumRemoved(songB, 2, true);
      choice.setIdRemoved(songB, 0, "R:USRC1", true);

      expect(choice.removals).toEqual([
        { songId: songB, album: 2 },
        { songId: songB, album: 0, id: "R:USRC1" },
      ]);
      expect(choice.isDirty).toBe(true);
    });
  });

  describe("deletions", () => {
    test("a song to delete is left out, can't name the merge, and has nothing removed", () => {
      const choice = choiceFor(featCluster);
      choice.pickName("title", songB);
      choice.setIdRemoved(songB, 2, "I:111", true);
      choice.setIdRemoved(songA, 0, "S:sp1", true);

      choice.setDeleted(songB, true);

      expect(choice.isDeleted(songB)).toBe(true);
      expect(choice.groupOf(songB)).toBe(leaveOut);
      expect(choice.canName(songB)).toBe(false);
      expect(choice.names.title).toBeNull();
      expect(choice.removals).toEqual([{ songId: songA, album: 0, id: "S:sp1" }]);
      expect(choice.isDirty).toBe(true);
    });

    test("not deleting it after all gives it its group back", () => {
      const choice = choiceFor(rejectedCluster);
      choice.setGroup(songD, "B");
      choice.setDeleted(songD, true);

      choice.setDeleted(songD, false);

      expect(choice.groupOf(songD)).toBe("B");
    });

    test("are sent whatever the action, with the song out of every group", () => {
      const choice = choiceFor(rejectedCluster);
      choice.setDeleted(songE, true);

      const request = choice.toRequest();

      expect(request.action).toBe("Leave");
      expect(request.deletions).toEqual([songE]);
      expect(request.songs.at(-1)).toEqual({ songId: songE, group: "" });
    });
  });

  describe("lookupIds", () => {
    test("are the songs' Spotify and iTunes ids, once each", () => {
      const cluster = new ClusterChoice(TypedJSON.parse(featCluster, SharedIdReviewCluster)!)
        .cluster;

      expect(lookupIds(cluster.songs)).toEqual(["S:sp1", "I:111"]);
      expect(lookupIds([])).toEqual([]);
    });
  });

  describe("toRequest", () => {
    test("a default merge sends every song in A and the proposal's names", () => {
      expect(choiceFor(featCluster).toRequest()).toEqual({
        action: "Merge",
        candidateIds: [11],
        songs: [
          { songId: songA, group: "A" },
          { songId: songB, group: "A" },
        ],
        titleFrom: null,
        artistFrom: null,
        artistsFrom: null,
        removals: [],
        deletions: [],
      });
    });

    test("a split merge sends the groups and the picks", () => {
      const choice = choiceFor(rejectedCluster);
      choice.setAction("Merge");
      choice.setGroup(songD, "B");
      choice.setGroup(songE, leaveOut);
      choice.pickName("artist", songD);

      const request = choice.toRequest();

      expect(request.songs).toEqual([
        { songId: songC, group: "A" },
        { songId: songD, group: "B" },
        { songId: songE, group: "" },
      ]);
      expect(request.titleFrom).toBe(songC);
      expect(request.artistFrom).toBe(songD);
      expect(request.candidateIds).toEqual([21, 22]);
    });

    test("Not the same sends no names, but still sends removals", () => {
      const choice = choiceFor(rejectedCluster);
      choice.setAction("Reject");
      choice.setIdRemoved(songD, 0, "S:sp9", true);

      const request = choice.toRequest();

      expect(request.action).toBe("Reject");
      expect([request.titleFrom, request.artistFrom, request.artistsFrom]).toEqual([
        null,
        null,
        null,
      ]);
      expect(request.removals).toEqual([{ songId: songD, album: 0, id: "S:sp9" }]);
    });

    test("Leave for later is sent as is", () => {
      expect(choiceFor(rejectedCluster).toRequest().action).toBe("Leave");
    });
  });
});
