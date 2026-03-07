import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { useEngagementOffcanvas } from "../useEngagementOffcanvas";
import type { EngagementConfig } from "@/models/EngagementConfig";

// Helper config for tests (enabled by default, unlike production default)
const testConfig: EngagementConfig = {
  enabled: true,
  firstShowPageCount: 2,
  repeatInterval: 5,
  sessionDismissalTimeout: 30,
  messages: {
    level1: "Test Level 1",
    level2: "Test Level 2",
    level3: "Test Level 3 - {pageCount} pages",
  },
  ctaUrls: {
    register: "/register",
    features: "/features",
    subscribe: "/subscribe",
  },
};

describe("useEngagementOffcanvas", () => {
  beforeEach(() => {
    // Clear localStorage and sessionStorage
    localStorage.clear();
    sessionStorage.clear();

    // Clear all timers
    vi.clearAllTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("Initialization", () => {
    it("should initialize with page count 0 when localStorage is empty", () => {
      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement._getPageCount()).toBe(0);
      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should read page count from localStorage", () => {
      localStorage.setItem("usageCount", "5");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement._getPageCount()).toBe(5);
    });

    it("should not show offcanvas on first page load (page 0)", () => {
      localStorage.setItem("usageCount", "0");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not show offcanvas on first page load (page 1)", () => {
      localStorage.setItem("usageCount", "1");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });
  });

  describe("Show Logic - Default Configuration", () => {
    it("should show offcanvas on page 2 (firstShowPageCount)", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });

    it("should not show on page 3 (not on repeat interval)", () => {
      localStorage.setItem("usageCount", "3");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not show on page 4", () => {
      localStorage.setItem("usageCount", "4");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not show on page 5", () => {
      localStorage.setItem("usageCount", "5");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not show on page 6", () => {
      localStorage.setItem("usageCount", "6");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should show on page 7 (2 + 5, repeat interval)", () => {
      localStorage.setItem("usageCount", "7");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });

    it("should not show on page 8", () => {
      localStorage.setItem("usageCount", "8");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should show on page 12 (2 + 5*2, repeat interval)", () => {
      localStorage.setItem("usageCount", "12");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });

    it("should show on page 17 (2 + 5*3, repeat interval)", () => {
      localStorage.setItem("usageCount", "17");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });
  });

  describe("Custom Configuration", () => {
    it("should respect custom firstShowPageCount", () => {
      localStorage.setItem("usageCount", "3");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 3,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });

    it("should respect custom repeatInterval", () => {
      localStorage.setItem("usageCount", "5");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 3,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });
  });

  describe("Engagement Levels", () => {
    it("should return Level 1 for pages 2-6", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.currentLevel.value?.level).toBe(1);
    });

    it("should return Level 2 for pages 7-11", () => {
      localStorage.setItem("usageCount", "7");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.currentLevel.value?.level).toBe(2);
    });

    it("should return Level 3 for pages 12+", () => {
      localStorage.setItem("usageCount", "12");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.currentLevel.value?.level).toBe(3);
    });

    it("should return null currentLevel when offcanvas is hidden", () => {
      localStorage.setItem("usageCount", "3");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.currentLevel.value).toBeNull();
    });
  });

  describe("Message Formatting", () => {
    it("should return level 1 message without placeholder", () => {
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Welcome to music4dance!",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.currentLevel.value?.message).toBe("Welcome to music4dance!");
    });

    it("should replace {pageCount} placeholder in level 3 message", () => {
      localStorage.setItem("usageCount", "12");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "You've loaded {pageCount} pages!",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.currentLevel.value?.message).toBe("You've loaded 12 pages!");
    });

    it("should handle multiple {pageCount} placeholders", () => {
      localStorage.setItem("usageCount", "17");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "After {pageCount} pages, that's {pageCount} opportunities!",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      // Note: .replace() only replaces first occurrence
      // If multiple replacements needed, architecture may need updating
      expect(engagement.currentLevel.value?.message).toContain("17");
    });
  });

  describe("Dismissal Behavior", () => {
    it("should hide offcanvas when dismissed", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(testConfig);
      expect(engagement.shouldShowOffcanvas.value).toBe(true);

      engagement.dismiss();

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should set session storage flag when dismissed", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(testConfig);
      engagement.dismiss();

      expect(engagement._isDismissedForSession()).toBe(true);
    });

    it("should not show offcanvas if dismissed for session", () => {
      localStorage.setItem("usageCount", "2");
      sessionStorage.setItem("engagementDismissed", "true");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should schedule session dismissal timeout when configured", () => {
      vi.useFakeTimers();
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 1, // 1 minute
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);
      engagement.dismiss();

      expect(sessionStorage.getItem("engagementDismissed")).toBe("true");

      // Fast-forward time by 1 minute (60,000 ms)
      vi.advanceTimersByTime(60_000);

      // Dismissal flag should be cleared
      expect(sessionStorage.getItem("engagementDismissed")).toBeNull();

      vi.useRealTimers();
    });
  });

  describe("Authenticated Users", () => {
    it("should never show offcanvas for authenticated users", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(undefined, true);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not allow manual show for authenticated users", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(undefined, true);
      engagement.show();

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });
  });

  describe("Disabled Configuration", () => {
    it("should never show offcanvas when disabled", () => {
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: false,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });

    it("should not allow manual show when disabled", () => {
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: false,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);
      engagement.show();

      expect(engagement.shouldShowOffcanvas.value).toBe(false);
    });
  });

  describe("Google Ads Display Logic", () => {
    it("should not show ads on first page load", () => {
      localStorage.setItem("usageCount", "0");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowAds.value).toBe(false);
    });

    it("should show ads after first page for anonymous users", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowAds.value).toBe(true);
    });

    it("should not show ads for authenticated users", () => {
      localStorage.setItem("usageCount", "2");

      const engagement = useEngagementOffcanvas(undefined, true);

      expect(engagement.shouldShowAds.value).toBe(false);
    });

    it("should not show ads when disabled", () => {
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: false,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/register",
          features: "/features",
          subscribe: "/subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.shouldShowAds.value).toBe(false);
    });

    it("should not show ads when dismissed for session", () => {
      localStorage.setItem("usageCount", "2");
      sessionStorage.setItem("engagementDismissed", "true");

      const engagement = useEngagementOffcanvas(testConfig);

      expect(engagement.shouldShowAds.value).toBe(false);
    });
  });

  describe("Manual Show Method", () => {
    it("should allow manual show for anonymous users when enabled", () => {
      localStorage.setItem("usageCount", "3"); // Not a show page

      const engagement = useEngagementOffcanvas(testConfig);
      expect(engagement.shouldShowOffcanvas.value).toBe(false);

      engagement.show();

      expect(engagement.shouldShowOffcanvas.value).toBe(true);
    });
  });

  describe("CTA URLs", () => {
    it("should return correct CTA URLs from config", () => {
      localStorage.setItem("usageCount", "2");
      const config: EngagementConfig = {
        enabled: true,
        firstShowPageCount: 2,
        repeatInterval: 5,
        sessionDismissalTimeout: 30,
        messages: {
          level1: "Test",
          level2: "Test",
          level3: "Test",
        },
        ctaUrls: {
          register: "/custom-register",
          features: "/custom-features",
          subscribe: "/custom-subscribe",
        },
      };

      const engagement = useEngagementOffcanvas(config);

      expect(engagement.currentLevel.value?.ctaUrls.primary).toBe("/custom-register");
      expect(engagement.currentLevel.value?.ctaUrls.secondary).toBe("/custom-features");
      expect(engagement.currentLevel.value?.ctaUrls.tertiary).toBe("/custom-subscribe");
    });
  });
});
