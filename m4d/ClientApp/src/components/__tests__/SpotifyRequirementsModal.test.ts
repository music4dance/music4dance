import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, VueWrapper } from "@vue/test-utils";
import SpotifyRequirementsModal from "../SpotifyRequirementsModal.vue";
import { BModal } from "bootstrap-vue-next";

// Mock getMenuContext
vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => ({
    isLoggedIn: false,
    isPremium: false,
    canSpotify: false,
    userId: null,
    getAccountLink: (action: string, returnUrl?: string) =>
      `/identity/account/${action}${returnUrl ? `?returnUrl=${returnUrl}` : ""}`,
  }),
}));

describe("SpotifyRequirementsModal", () => {
  let wrapper: VueWrapper;

  const createWrapper = (props = {}) => {
    return mount(SpotifyRequirementsModal, {
      props: {
        featureName: "Add to Spotify Playlist",
        modelValue: true,
        ...props,
      },
      global: {
        components: {
          BModal,
        },
        stubs: {
          BModal: {
            template: `
              <div class="modal-stub">
                <div class="modal-title">{{ title }}</div>
                <slot></slot>
              </div>
            `,
            props: ["modelValue", "title", "okOnly", "okTitle"],
          },
        },
      },
    });
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("Checklist Display", () => {
    it("shows all three requirements unchecked for anonymous user", () => {
      wrapper = createWrapper({
        isAuthenticated: false,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const checkCircles = wrapper.findAll(".icon-check.text-success");
      const emptyCircles = wrapper.findAll(".icon-check.text-muted");

      expect(checkCircles.length).toBe(0);
      expect(emptyCircles.length).toBe(3);
      expect(wrapper.text()).toContain("Be signed in");
      expect(wrapper.text()).toContain("Have a premium subscription");
      expect(wrapper.text()).toContain("Have associated a Spotify account");
    });

    it("shows first requirement checked for authenticated non-premium user", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const checkCircles = wrapper.findAll(".icon-check.text-success");
      const emptyCircles = wrapper.findAll(".icon-check.text-muted");

      expect(checkCircles.length).toBe(1);
      expect(emptyCircles.length).toBe(2);
    });

    it("shows two requirements checked for authenticated premium user without Spotify", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: false,
      });

      const checkCircles = wrapper.findAll(".icon-check.text-success");
      const emptyCircles = wrapper.findAll(".icon-check.text-muted");

      expect(checkCircles.length).toBe(2);
      expect(emptyCircles.length).toBe(1);
    });

    it("shows all requirements checked for fully authorized user", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: true,
      });

      const checkCircles = wrapper.findAll(".icon-check");
      // Success alert has 1 check icon
      expect(checkCircles.length).toBeGreaterThanOrEqual(1);
      expect(wrapper.text()).toContain("All requirements met!");
    });
  });

  describe("Action Links", () => {
    it("shows sign-in button for anonymous user", () => {
      wrapper = createWrapper({
        isAuthenticated: false,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const signInLink = wrapper.find('a[href*="login"]');
      expect(signInLink.exists()).toBe(true);
      expect(signInLink.text()).toBe("Sign In");
    });

    it("shows premium subscription link for non-premium user", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const premiumLink = wrapper.find('a[href="/home/contribute"]');
      expect(premiumLink.exists()).toBe(true);
      expect(premiumLink.text()).toBe("Sign Up for Premium");
    });

    it("shows Spotify connect link for premium user without OAuth", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: false,
      });

      const spotifyLink = wrapper.find('a[href*="externallogins"]');
      expect(spotifyLink.exists()).toBe(true);
      expect(spotifyLink.text()).toBe("Connect Spotify Account");
    });

    it("does not show Spotify connect link for anonymous user", () => {
      wrapper = createWrapper({
        isAuthenticated: false,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const spotifyLink = wrapper.find('a[href*="externallogins"]');
      expect(spotifyLink.exists()).toBe(false);
      expect(wrapper.text()).toContain("Sign in first to connect your Spotify account");
    });
  });

  describe("Next Step Guidance", () => {
    it("shows sign-in as next step for anonymous user", () => {
      wrapper = createWrapper({
        isAuthenticated: false,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      expect(wrapper.text()).toContain("Next step:");
      expect(wrapper.text()).toContain("Sign in to your music4dance account");
    });

    it("shows premium upgrade as next step for authenticated non-premium user", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      expect(wrapper.text()).toContain("Next step:");
      expect(wrapper.text()).toContain("Upgrade to a premium subscription");
    });

    it("shows Spotify connect as next step for premium user without OAuth", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: false,
      });

      expect(wrapper.text()).toContain("Next step:");
      expect(wrapper.text()).toContain("Connect your Spotify account");
    });

    it("does not show next step for fully authorized user", () => {
      wrapper = createWrapper({
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: true,
      });

      const nextStepAlert = wrapper.find(".alert-info");
      expect(nextStepAlert.exists()).toBe(false);
    });
  });

  describe("Help Links", () => {
    it("includes help links for each requirement", () => {
      wrapper = createWrapper({
        isAuthenticated: false,
        isPremium: false,
        hasSpotifyOAuth: false,
      });

      const helpLinks = wrapper.findAll('a[href*="music4dance.blog"]');
      // Account, subscriptions, external account help links + main documentation link at bottom
      expect(helpLinks.length).toBeGreaterThanOrEqual(3);
    });
  });

  describe("Modal Properties", () => {
    it("sets correct modal title based on feature name", () => {
      wrapper = createWrapper({
        featureName: "Test Feature",
      });

      expect(wrapper.text()).toContain("Test Feature Requirements");
    });

    it("uses feature name throughout modal content", () => {
      wrapper = createWrapper({
        featureName: "Custom Playlist Export",
        isAuthenticated: true,
        isPremium: true,
        hasSpotifyOAuth: true,
      });

      expect(wrapper.text()).toContain("Custom Playlist Export");
    });
  });
});
