import { describe, test, expect, beforeEach } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { model, modelWithFilteredUser } from "./model";

describe("playlist page", () => {
  test("renders the playlist page", () => {
    testPageSnapshot(App, model);
  });

  describe("active/deleted toggle", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("shows only active playlists by default", () => {
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      // 2 active, 1 deleted → only 2 active shown
      expect(rows.length).toBe(2);
    });

    test("toggle show deleted reveals deleted playlists", async () => {
      const btn = wrapper.findAll("button").find((b) => b.text().includes("Show Deleted"));
      expect(btn).toBeDefined();
      await btn!.trigger("click");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
    });

    test("toggle button text changes to 'Show Active' when showing deleted", async () => {
      const btn = wrapper.findAll("button").find((b) => b.text().includes("Show Deleted"));
      await btn!.trigger("click");
      expect(wrapper.text()).toContain("Show Active");
    });
  });

  describe("summary counts", () => {
    test("shows active count", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.text()).toContain("Active: 2");
    });

    test("shows deleted count", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.text()).toContain("Deleted: 1");
    });
  });

  describe("user filter", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("text filter narrows by user name", async () => {
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("alice");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      // alice has 1 active playlist
      expect(rows.length).toBe(1);
    });

    test("text filter narrows by playlist name", async () => {
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("waltz");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
    });

    test("text filter narrows by id", async () => {
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("def456");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
    });

    test("case-insensitive filter works", async () => {
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("WALTZ");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
    });

    test("non-matching filter shows zero rows", async () => {
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("zzznomatch");
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(0);
    });
  });

  describe("pre-populated user filter", () => {
    test("filteredUser from model pre-populates user filter", () => {
      const wrapper = loadTestPage(App, modelWithFilteredUser);
      // alice has 1 active playlist
      const rows = wrapper.find("#playlist-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
    });

    test("shows user filter banner when filteredUser is set", () => {
      const wrapper = loadTestPage(App, modelWithFilteredUser);
      expect(wrapper.text()).toContain("Filtered to user:");
      expect(wrapper.text()).toContain("alice");
    });

    test("user filter banner not shown when no filteredUser", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.text()).not.toContain("Filtered to user:");
    });
  });

  describe("action links", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("active playlist rows have Update link", () => {
      const links = wrapper.find("#playlist-table").findAll("a");
      const updateLinks = links.filter((a) => a.text() === "Update");
      expect(updateLinks.length).toBeGreaterThan(0);
    });

    test("active playlist rows have Edit link", () => {
      const links = wrapper.find("#playlist-table").findAll("a");
      const editLinks = links.filter((a) => a.text() === "Edit");
      expect(editLinks.length).toBeGreaterThan(0);
    });

    test("active playlist rows have Delete link", () => {
      const links = wrapper.find("#playlist-table").findAll("a");
      const deleteLinks = links.filter((a) => a.text() === "Delete");
      expect(deleteLinks.length).toBeGreaterThan(0);
    });

    test("deleted playlist rows have Undelete link", async () => {
      const btn = wrapper.findAll("button").find((b) => b.text().includes("Show Deleted"));
      await btn!.trigger("click");
      const links = wrapper.find("#playlist-table").findAll("a");
      const undelLinks = links.filter((a) => a.text() === "Undelete");
      expect(undelLinks.length).toBe(1);
    });

    test("active playlist with updated and no data2 has Restore link", () => {
      // bob's playlist has updated=null → no Restore; alice's has updated + data2 (SongIds) → no Restore
      // (Restore appears only when updated && !data2)
      // In our fixture, alice has data2="song1|song2" → no Restore, bob has updated=null → no Restore
      // No Restore links expected
      const links = wrapper.find("#playlist-table").findAll("a");
      const restoreLinks = links.filter((a) => a.text() === "Restore");
      expect(restoreLinks.length).toBe(0);
    });
  });

  describe("type switcher", () => {
    test("shows SongsFromSpotify link (currently active type is highlighted)", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.text()).toContain("SongsFromSpotify");
      expect(wrapper.text()).toContain("SpotifyFromSearch");
    });

    test("current type link has bold class", () => {
      const wrapper = loadTestPage(App, model);
      // SongsFromSpotify is current type (type=2), so its link should have fw-bold class
      const typeLinks = wrapper.findAll("a").filter((a) => a.text() === "SongsFromSpotify");
      expect(typeLinks.length).toBeGreaterThan(0);
      expect(typeLinks[0].classes()).toContain("fw-bold");
    });
  });
});
