import { describe, test, expect, beforeEach } from "vitest";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { model, adminModel, allUsersModel } from "./model";

describe("searches page", () => {
  test("renders the searches page snapshot", () => {
    testPageSnapshot(App, model);
  });

  test("renders the admin detail view snapshot", () => {
    testPageSnapshot(App, adminModel);
  });

  test("renders the all-users view snapshot", () => {
    testPageSnapshot(App, allUsersModel);
  });

  describe("basic (non-admin) view", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("shows search buttons for each row", () => {
      const searchLinks = wrapper.findAll("a.btn-success");
      expect(searchLinks.length).toBe(model.searches.length);
    });

    test("shows delete buttons for each row", () => {
      const deleteLinks = wrapper.findAll("a.btn-danger");
      expect(deleteLinks.length).toBe(model.searches.length);
    });

    test("shows page link when mostRecentPage is set", () => {
      const pageLinks = wrapper.findAll("a.btn-outline-success");
      expect(pageLinks.length).toBe(1);
      expect(pageLinks[0].text()).toContain("Page 2");
    });

    test("shows spotify icon for rows with spotify ID", () => {
      const spotifyIcons = wrapper.findAll('img[alt="Spotify Playlist"]');
      expect(spotifyIcons.length).toBe(1);
    });

    test("shows pagination when totalPages > 1", () => {
      expect(wrapper.find("ul.pagination").exists()).toBe(true);
    });

    test("shows delete-all button for user-scoped view", () => {
      expect(wrapper.find("a.btn-outline-danger").exists()).toBe(true);
      expect(wrapper.find("a.btn-outline-danger").text()).toContain("Clear My Search History");
    });

    test("does not show Toggle Details link for non-admin", () => {
      expect(wrapper.text()).not.toContain("Toggle Details");
    });

    test("shows Basic Search and Advanced Search links", () => {
      expect(wrapper.find("#saved-search").exists()).toBe(true);
      expect(wrapper.find("#advanced-search").exists()).toBe(true);
    });

    test("shows sort buttons", () => {
      const sortButtons = wrapper.findAll(".btn-group a");
      expect(sortButtons.length).toBeGreaterThanOrEqual(2);
      expect(sortButtons[0].text()).toBe("Most Popular");
      expect(sortButtons[1].text()).toBe("Most Recent");
    });

    test("Most Popular is active when sort is not recent", () => {
      const popularBtn = wrapper.findAll(".btn-group a")[0];
      expect(popularBtn.classes()).toContain("btn-primary");
      expect(popularBtn.classes()).not.toContain("btn-outline-primary");
    });
  });

  describe("admin detail view", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, adminModel);
    });

    test("shows Toggle Details link for admin", () => {
      expect(wrapper.text()).toContain("Toggle Details");
    });

    test("shows search table with detail columns", () => {
      const table = wrapper.find("#searches-table");
      expect(table.exists()).toBe(true);
      expect(wrapper.text()).toContain("Query");
      expect(wrapper.text()).toContain("User");
      expect(wrapper.text()).toContain("Created");
    });
  });

  describe("all-users view", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, allUsersModel);
    });

    test("does not show delete-all button for all-users view", () => {
      expect(wrapper.find("a.btn-outline-danger").exists()).toBe(false);
    });
  });

  describe("pagination", () => {
    test("shows correct page count in pagination nav", () => {
      const wrapper = loadTestPage(App, model);
      const pagination = wrapper.find("ul.pagination");
      expect(pagination.exists()).toBe(true);
      // Should show page 1 (active), ellipsis optional, page 3 at minimum
      expect(pagination.text()).toContain("1");
      expect(pagination.text()).toContain("3");
    });

    test("no pagination shown when totalPages is 1", () => {
      const singlePageModel = { ...model, totalPages: 1, page: 1 };
      const wrapper = loadTestPage(App, singlePageModel);
      expect(wrapper.find("ul.pagination").exists()).toBe(false);
    });
  });
});
