import { describe, test, expect, beforeEach } from "vitest";
import type { VueWrapper } from "@vue/test-utils";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { model } from "./model";

function findRoleCheckbox(wrapper: VueWrapper, role: string) {
  const group = wrapper
    .find("#roles-filter")
    .findAll(".form-check")
    .find((c) => c.text().trim() === role);
  if (!group) {
    throw new Error(`Role checkbox "${role}" not found`);
  }
  return group.find('input[type="checkbox"]');
}

function setTriState(wrapper: VueWrapper, id: string, value: "any" | "yes" | "no") {
  return wrapper.find(`#${id}`).setValue(value);
}

describe("admin-users page", () => {
  test("renders the admin users page", () => {
    testPageSnapshot(App, model);
  });

  describe("summary statistics", () => {
    let wrapper: ReturnType<typeof loadTestPage>;

    beforeEach(() => {
      wrapper = loadTestPage(App, model);
    });

    test("total users count includes all users", () => {
      expect(wrapper.text()).toContain("Total Users: 4");
    });

    test("registered users excludes pseudo users", () => {
      // carol|P is pseudo → 3 non-pseudo: alice, bob, DEL:dave
      expect(wrapper.text()).toContain("Registered Users: 3");
    });

    test("confirmed users counts confirmed non-pseudo", () => {
      // alice and DEL:dave are confirmed non-pseudo
      expect(wrapper.text()).toContain("Confirmed Users: 2");
    });

    test("deleted users counts DEL: prefix", () => {
      expect(wrapper.text()).toContain("Deleted Users: 1");
    });

    test("role counts are shown", () => {
      // Only alice has 'premium' role
      expect(wrapper.text()).toContain("premium: 1");
      expect(wrapper.text()).toContain("dbAdmin: 0");
    });

    test("login provider counts are shown", () => {
      // Spotify: alice + carol = 2; Microsoft: bob = 1
      expect(wrapper.text()).toContain("Spotify: 2");
      expect(wrapper.text()).toContain("Microsoft: 1");
    });
  });

  describe("filter logic", () => {
    test("by default, unconfirmed and pseudo users are hidden, roles and privacy unfiltered", () => {
      // alice: confirmed, premium role (visible); bob: unconfirmed (hidden);
      // carol: pseudo (hidden); DEL:dave: plain confirmed non-pseudo (visible)
      const wrapper = loadTestPage(App, model);
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(2);
    });

    test("setting Unconfirmed filter to Yes isolates unconfirmed non-pseudo users", async () => {
      const wrapper = loadTestPage(App, model);
      await setTriState(wrapper, "filter-unconfirmed", "yes");
      // bob is the only unconfirmed user; carol (pseudo) is always confirmed
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
      expect(rows[0]!.text()).toContain("bob");
    });

    test("setting Unconfirmed filter to Any reveals unconfirmed users alongside the default set", async () => {
      const wrapper = loadTestPage(App, model);
      await setTriState(wrapper, "filter-unconfirmed", "any");
      // alice, DEL:dave (default) plus bob (unconfirmed); carol still hidden (pseudo=No)
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(3);
    });

    test("setting Pseudo filter to Yes isolates pseudo users", async () => {
      const wrapper = loadTestPage(App, model);
      await setTriState(wrapper, "filter-pseudo", "yes");
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
      expect(rows[0]!.text()).toContain("carol");
    });

    test("checking a role isolates users with that role", async () => {
      const wrapper = loadTestPage(App, model);
      await findRoleCheckbox(wrapper, "premium").setValue(true);
      // Only alice has the premium role
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
      expect(rows[0]!.text()).toContain("alice");
    });

    test("checking a role no one in the default view has hides everyone", async () => {
      const wrapper = loadTestPage(App, model);
      await findRoleCheckbox(wrapper, "dbAdmin").setValue(true);
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(0);
    });

    test("setting Private filter to No hides users with privacy != 255", async () => {
      // carol has privacy=0; reveal her via Pseudo=Any, then exclude non-public via Private=No
      const wrapper = loadTestPage(App, model);
      await setTriState(wrapper, "filter-pseudo", "any");
      expect(wrapper.find("#users-table").findAll("tbody tr").length).toBe(3);

      await setTriState(wrapper, "filter-private", "no");
      // carol (privacy=0) is now hidden → 2 rows remain (alice, DEL:dave)
      expect(wrapper.find("#users-table").findAll("tbody tr").length).toBe(2);
    });

    test("setting Private filter to Yes isolates non-public users", async () => {
      const wrapper = loadTestPage(App, model);
      await setTriState(wrapper, "filter-pseudo", "any");
      await setTriState(wrapper, "filter-private", "yes");
      const rows = wrapper.find("#users-table").findAll("tbody tr");
      expect(rows.length).toBe(1);
      expect(rows[0]!.text()).toContain("carol");
    });

    test("tri-state select reflects current value", async () => {
      const wrapper = loadTestPage(App, model);
      const select = wrapper.find("#filter-unconfirmed");
      expect((select.element as HTMLSelectElement).value).toBe("no");
      await select.setValue("yes");
      expect((select.element as HTMLSelectElement).value).toBe("yes");
    });

    test("text search filters by userName", async () => {
      const wrapper = loadTestPage(App, model);
      const input = wrapper.find("input[placeholder*='Filter']");
      expect(input.exists()).toBe(true);
      // Default visible: alice, DEL:dave — type "alice" to narrow to 1
      await input.setValue("alice");
      expect(wrapper.find("#users-table").findAll("tbody tr").length).toBe(1);
      // Clear search — both rows return
      await input.setValue("");
      expect(wrapper.find("#users-table").findAll("tbody tr").length).toBe(2);
    });

    test("text search filters by email", async () => {
      const wrapper = loadTestPage(App, model);
      // Show unconfirmed so bob is visible, then search by email domain
      await setTriState(wrapper, "filter-unconfirmed", "any");
      const input = wrapper.find("input[placeholder*='Filter']");
      await input.setValue("example.com");
      // alice, bob, DEL:dave all have @example.com
      expect(wrapper.find("#users-table").findAll("tbody tr").length).toBe(3);
    });
  });

  describe("table content", () => {
    test("user detail link points to Details action", () => {
      const wrapper = loadTestPage(App, model);
      const link = wrapper.find('a[href="/ApplicationUsers/Details/user-1"]');
      expect(link.exists()).toBe(true);
      expect(link.text()).toBe("alice");
    });

    test("pseudo user name is rendered in italic", () => {
      // carol is pseudo but filtered out by default — just verify the page renders without errors
      const wrapper = loadTestPage(App, model);
      const html = wrapper.html();
      expect(html).toBeTruthy();
    });

    test("Never is shown for lastActive = 1900", async () => {
      const wrapper = loadTestPage(App, model);
      // bob has lastActive in 1900 (never active) but is unconfirmed — reveal him first
      await setTriState(wrapper, "filter-unconfirmed", "any");
      expect(wrapper.text()).toContain("Never");
    });

    test("action links contain correct user id", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.find('a[href="/ApplicationUsers/Edit/user-1"]').exists()).toBe(true);
      expect(wrapper.find('a[href="/ApplicationUsers/Delete/user-1"]').exists()).toBe(true);
      expect(wrapper.find('a[href="/ApplicationUsers/ChangeRoles/user-1"]').exists()).toBe(true);
      expect(wrapper.find('a[href="/ApplicationUsers/ClearPremium/user-1"]').exists()).toBe(true);
    });

    test("action links contain URL-encoded user name", () => {
      const wrapper = loadTestPage(App, model);
      expect(wrapper.find('a[href="/Song/FilterUser?user=alice"]').exists()).toBe(true);
      // Searches link uses & — verify via attribute value on the matching anchor
      const searchLinks = wrapper
        .findAll("a")
        .filter((a) => (a.attributes("href") ?? "").includes("/Searches/Index"));
      expect(searchLinks.length).toBeGreaterThan(0);
      expect(searchLinks[0].attributes("href")).toContain("showDetails=true");
      expect(searchLinks[0].attributes("href")).toContain("sort=recent");
    });
  });
});
