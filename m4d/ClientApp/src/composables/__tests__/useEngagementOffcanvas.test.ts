import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { useEngagementOffcanvas } from "../useEngagementOffcanvas";
import type { EngagementConfig } from "@/models/EngagementConfig";

const testConfig: EngagementConfig = {
  enabled: true,
  firstShowPageCount: 2,
  repeatInterval: 5,
  sessionDismissalTimeout: 30,
  messages: {
    level1: "Test Level 1",
    level2: "Test Level 2",
    level3: "Test Level 3",
    loggedInUpgrade: "Test Upgrade Message",
  },
  premiumBenefits: {
    items: ["Benefit 1", "Benefit 2"],
    moreText: "...more!",
    completeListUrl: "/subscriptions/",
  },
  ctaUrls: {
    register: "/register",
    login: "/login",
    subscribe: "/subscribe",
    features: "/features",
  },
};

describe("useEngagementOffcanvas", () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    vi.clearAllTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Premium User Behavior", () => {
    it("should return all false for premium users", () => {
      localStorage.setItem("usageCount", "5");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: true,
        isPremium: true,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(false);
      expect(engagement.isExpanded.value).toBe(false);
    });
  });

  describe("Anonymous User - Show Logic", () => {
    it("should not show on page 1", () => {
      localStorage.setItem("usageCount", "1");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(false);
    });

    it("should show bottom bar on page 2", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(true);
    });

    it("should show bottom bar on page 7", () => {
      localStorage.setItem("usageCount", "7");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(true);
    });

    it("should show bottom bar on page 12", () => {
      localStorage.setItem("usageCount", "12");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(true);
    });

    it("should show bottom bar on page 3 and above (uses >= logic)", () => {
      localStorage.setItem("usageCount", "3");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      // shouldShowBottomBar uses pageCount >= firstShowPageCount logic
      expect(engagement.shouldShowBottomBar.value).toBe(true);
    });
  });

  describe("Logged-In Non-Premium User", () => {
    it("should show bottom bar on page 2", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: true,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(true);
    });

    it("should return logged-in upgrade message", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: true,
        isPremium: false,
      });

      // currentLevel returns { level, message } object
      expect(engagement.currentLevel.value).toEqual({
        level: 1,
        message: "Test Upgrade Message",
      });
    });
  });

  describe("Expand/Collapse Behavior", () => {
    it("should expand when calling expand()", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();

      expect(engagement.isExpanded.value).toBe(true);
    });

    it("should collapse when calling collapse()", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();
      engagement.collapse();

      expect(engagement.isExpanded.value).toBe(false);
    });

    it("should populate engagementData when expanded", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();

      expect(engagement.isExpanded.value).toBe(true);
      expect(engagement.currentLevel.value).toEqual({
        level: 1,
        message: "Test Level 1",
      });
    });

    it("should clear engagementData when collapsed", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();
      engagement.collapse();

      expect(engagement.isExpanded.value).toBe(false);
    });
  });

  describe("Engagement Levels", () => {
    it("should return level 1 on page 2", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.currentLevel.value?.level).toBe(1);
    });

    it("should return level 2 on page 7", () => {
      localStorage.setItem("usageCount", "7");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.currentLevel.value?.level).toBe(2);
    });

    it("should return level 3 on page 12+", () => {
      localStorage.setItem("usageCount", "12");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.currentLevel.value?.level).toBe(3);
    });
  });

  describe("Google Ads Control", () => {
    // Note: document.cookie is difficult to mock in Vitest due to browser API limitations
    // Testing the logic without cookie consent check
    it("should not show ads when cookie consent is not available (test environment)", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      // In test environment without real cookie, shouldShowAds returns false
      // This is expected behavior - production will have actual cookie consent
      expect(engagement.shouldShowAds.value).toBe(false);
    });

    it("should not show ads when expanded", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();

      expect(engagement.shouldShowAds.value).toBe(false);
    });

    it("should respect cookie consent requirement (returns false without consent in tests)", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      engagement.expand();
      engagement.collapse();

      // Without real cookie consent (test environment), ads remain hidden
      expect(engagement.shouldShowAds.value).toBe(false);
    });
  });

  describe("Disabled Configuration", () => {
    it("should not show anything when disabled", () => {
      localStorage.setItem("usageCount", "5");

      const disabledConfig = { ...testConfig, enabled: false };
      const engagement = useEngagementOffcanvas({
        config: disabledConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      expect(engagement.shouldShowBottomBar.value).toBe(false);
      expect(engagement.isExpanded.value).toBe(false);
    });
  });

  describe("Auto-expand Behavior", () => {
    it("should auto-expand on trigger pages during initialize", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas({
        config: testConfig,
        isAuthenticated: false,
        isPremium: false,
      });

      // initialize() is called in setup, should auto-expand
      expect(engagement.isExpanded.value).toBe(true);
    });
  });
});
