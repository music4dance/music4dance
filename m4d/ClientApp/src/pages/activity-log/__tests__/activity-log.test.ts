import { describe, test, expect, beforeEach } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { model, singlePageModel } from "./model";

describe("activity-log page", () => {
  test("renders the activity log page snapshot", () => {
    testPageSnapshot(App, model);
  });

  test("renders the single-page view snapshot", () => {
    testPageSnapshot(App, singlePageModel);
  });

  describe("table content", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("renders the activity log table", () => {
      const table = wrapper.find("#activity-log-table");
      expect(table.exists()).toBe(true);
    });

    test("shows all entries", () => {
      const rows = wrapper.findAll("tbody tr");
      expect(rows.length).toBe(model.entries.length);
    });

    test("shows entry data", () => {
      expect(wrapper.text()).toContain("alice");
      expect(wrapper.text()).toContain("Login");
      expect(wrapper.text()).toContain("bob");
      expect(wrapper.text()).toContain("EditSong");
    });

    test("shows no user placeholder for null userName", () => {
      expect(wrapper.text()).toContain("no user");
    });
  });

  describe("pagination", () => {
    test("shows pagination when totalPages > 1", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.find("ul.pagination").exists()).toBe(true);
    });

    test("shows correct page numbers in nav", () => {
      const wrapper = loadTestPage(App, model);
      const pagination = wrapper.find("ul.pagination");
      expect(pagination.text()).toContain("1");
      expect(pagination.text()).toContain("5");
    });

    test("no pagination shown when totalPages is 1", () => {
      const wrapper = loadTestPage(App, singlePageModel);
      expect(wrapper.find("ul.pagination").exists()).toBe(false);
    });
  });
});
