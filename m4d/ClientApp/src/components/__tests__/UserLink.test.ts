import { describe, it, expect } from "vitest";
import { shallowMount } from "@vue/test-utils";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import UserLink from "../UserLink.vue";

setupTestEnvironment();

describe("UserLink.vue", () => {
  describe("initial rendering", () => {
    it("renders a link for a regular user", () => {
      const wrapper = shallowMount(UserLink, { props: { user: "dwgray" } });
      expect(wrapper.find("a").exists()).toBe(true);
      expect(wrapper.find("a").attributes("href")).toContain("dwgray");
    });

    it("renders strong (no link) for an algorithmic user", () => {
      const wrapper = shallowMount(UserLink, { props: { user: "batch-a|P" } });
      expect(wrapper.find("a").exists()).toBe(false);
      expect(wrapper.find("strong").exists()).toBe(true);
    });

    it("applies pseudo class for a proxy user", () => {
      const wrapper = shallowMount(UserLink, { props: { user: "dwgray|P" } });
      expect(wrapper.find("a").classes()).toContain("pseudo");
    });
  });

  describe("prop reactivity — BUG REPRO", () => {
    // UserLink.vue previously created userQuery/userLink/userClasses as plain
    // variables at setup() time. When `user` prop changed (because Vue reused
    // the component instance via :key="index" in SongHistoryViewer's v-for),
    // those stale values were still displayed. This caused the user name shown
    // in each SongChangeViewer row to remain unchanged after the
    // "Include automated" toggle was switched.

    it("updates display name when user prop changes from human to algorithmic", async () => {
      const wrapper = shallowMount(UserLink, { props: { user: "dwgray" } });

      // Before: should show a link for the human user
      expect(wrapper.find("a").exists()).toBe(true);

      await wrapper.setProps({ user: "batch-a|P" });

      // After: algorithmic user must render as <strong>, not <a>
      expect(wrapper.find("a").exists()).toBe(false);
      expect(wrapper.find("strong").exists()).toBe(true);
    });

    it("updates display name when user prop changes from algorithmic to human", async () => {
      const wrapper = shallowMount(UserLink, { props: { user: "batch-a|P" } });

      expect(wrapper.find("a").exists()).toBe(false);
      expect(wrapper.find("strong").exists()).toBe(true);

      await wrapper.setProps({ user: "dwgray" });

      expect(wrapper.find("a").exists()).toBe(true);
      expect(wrapper.find("a").attributes("href")).toContain("dwgray");
    });

    it("updates href when user name changes between two human users", async () => {
      const wrapper = shallowMount(UserLink, { props: { user: "alice" } });
      expect(wrapper.find("a").attributes("href")).toContain("alice");

      await wrapper.setProps({ user: "bob" });

      expect(wrapper.find("a").attributes("href")).toContain("bob");
      expect(wrapper.find("a").attributes("href")).not.toContain("alice");
    });

    it("updates pseudo class when switching from proxy to non-proxy user", async () => {
      const wrapper = shallowMount(UserLink, { props: { user: "dwgray|P" } });
      expect(wrapper.find("a").classes()).toContain("pseudo");

      await wrapper.setProps({ user: "dwgray" });

      expect(wrapper.find("a").classes()).not.toContain("pseudo");
    });
  });
});
