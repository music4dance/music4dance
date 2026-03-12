/**
 * Engagement offcanvas composable
 * Determines when to show engagement prompts to users (anonymous or logged-in non-premium)
 */

import { ref, computed } from "vue";
import type { EngagementConfig } from "@/models/EngagementConfig";
import { defaultEngagementConfig } from "@/models/EngagementConfig";

const STORAGE_KEY_USAGE_COUNT = "usageCount";
const SESSION_KEY_DISMISSED = "engagementDismissed";

export interface EngagementLevel {
  level: 1 | 2 | 3;
  message: string;
}

export interface UseEngagementOffcanvasOptions {
  config?: EngagementConfig;
  isAuthenticated: boolean;
  isPremium: boolean;
}

/**
 * Composable for managing engagement offcanvas display logic
 * @param options - Configuration options
 * @returns Object with reactive state and control methods
 *
 * @example
 * ```typescript
 * const engagement = useEngagementOffcanvas({
 *   config: menuContext.engagementConfig,
 *   isAuthenticated: !!menuContext.userName,
 *   isPremium: menuContext.isPremium
 * });
 *
 * // Check if bottom bar should show
 * if (engagement.shouldShowBottomBar.value) {
 *   // Display bottom bar
 * }
 *
 * // User clicks to expand
 * engagement.expand();
 *
 * // User collapses
 * engagement.collapse();
 * ```
 */
export function useEngagementOffcanvas(options: UseEngagementOffcanvasOptions) {
  const { config, isAuthenticated, isPremium } = options;

  // Merge provided config with defaults
  const finalConfig = { ...defaultEngagementConfig, ...config };

  // Never show for premium users
  if (isPremium) {
    return {
      shouldShowBottomBar: ref(false),
      shouldShowOffcanvas: ref(false),
      isExpanded: ref(false),
      currentLevel: computed(() => null),
      shouldShowAds: computed(() => true), // Ads OK for premium users
      expand: () => {},
      collapse: () => {},
      // Expose for testing
      _getPageCount: () => 0,
      _isDismissedForSession: () => false,
    };
  }

  // Reactive state
  const isExpanded = ref(false);
  const pageCount = ref(0);

  /**
   * Get current page count from localStorage
   */
  function getPageCount(): number {
    const stored = localStorage.getItem(STORAGE_KEY_USAGE_COUNT);
    return stored ? parseInt(stored, 10) : 0;
  }

  /**
   * Check if user has dismissed for this session
   */
  function isDismissedForSession(): boolean {
    return sessionStorage.getItem(SESSION_KEY_DISMISSED) === "true";
  }

  /**
   * Calculate whether offcanvas should be shown based on page count and configuration
   */
  function calculateShouldShow(count: number): boolean {
    // Never show if disabled or dismissed for session
    if (!finalConfig.enabled || isDismissedForSession()) {
      return false;
    }

    // Check feature flags for anonymous vs logged-in
    if (!isAuthenticated && finalConfig.showForAnonymous === false) {
      return false;
    }

    if (isAuthenticated && finalConfig.showForLoggedIn === false) {
      return false;
    }

    // Never show on first page load (pages 0 or 1)
    if (count <= 1) {
      return false;
    }

    // Show on configured first show page (default: 2)
    if (count === finalConfig.firstShowPageCount) {
      return true;
    }

    // After first show, show on repeat interval (default: every 5 pages)
    if (count > finalConfig.firstShowPageCount) {
      const pagesSinceFirst = count - finalConfig.firstShowPageCount;
      return pagesSinceFirst % finalConfig.repeatInterval === 0;
    }

    return false;
  }

  /**
   * Determine engagement level based on page count
   * Level 1: Pages 2-6 (Awareness)
   * Level 2: Pages 7-11 (Consideration)
   * Level 3: Pages 12+ (Conversion)
   */
  function calculateEngagementLevel(count: number): 1 | 2 | 3 {
    if (count >= 12) return 3;
    if (count >= 7) return 2;
    return 1;
  }

  /**
   * Get message for current engagement level
   */
  function getMessage(level: 1 | 2 | 3): string {
    if (!isAuthenticated) {
      // Anonymous users - progressive message
      if (level === 1) {
        return finalConfig.messages.level1;
      } else if (level === 2) {
        return finalConfig.messages.level2;
      } else {
        return finalConfig.messages.level3;
      }
    } else {
      // Logged-in non-premium users - upgrade message
      return finalConfig.messages.loggedInUpgrade || "";
    }
  }

  /**
   * Initialize: Read page count and auto-expand if on trigger page
   */
  function initialize(): void {
    pageCount.value = getPageCount();

    // Auto-expand on trigger pages (2, 7, 12, etc.) if not already dismissed
    if (calculateShouldShow(pageCount.value)) {
      expand();
    }
  }

  /**
   * Expand offcanvas (user clicks bottom bar or auto-triggered)
   */
  function expand(): void {
    if (finalConfig.enabled && !isPremium) {
      isExpanded.value = true;

      // Pause Google Ads when expanded
      if (typeof window !== "undefined" && (window as any).adsbygoogle) {
        (window as any).adsbygoogle.pauseAdRequests = 1;
      }
    }
  }

  /**
   * Collapse offcanvas (user clicks "Maybe Later" or down arrow)
   */
  function collapse(): void {
    isExpanded.value = false;

    // Resume Google Ads when collapsed
    if (
      typeof window !== "undefined" &&
      (window as any).adsbygoogle &&
      (window as any).adsbygoogle.pauseAdRequests !== undefined
    ) {
      (window as any).adsbygoogle.pauseAdRequests = 0;
    }

    // Note: We don't store dismissal - bottom bar stays visible
  }

  /**
   * Dismiss for session (optional, for future use if needed)
   */
  function dismissForSession(): void {
    collapse();
    sessionStorage.setItem(SESSION_KEY_DISMISSED, "true");

    // If sessionDismissalTimeout is configured, clear the dismissal after timeout
    if (finalConfig.sessionDismissalTimeout > 0) {
      setTimeout(
        () => {
          sessionStorage.removeItem(SESSION_KEY_DISMISSED);
        },
        finalConfig.sessionDismissalTimeout * 60 * 1000,
      ); // Convert minutes to milliseconds
    }
  }

  // Computed properties
  const shouldShowBottomBar = computed(() => {
    if (!finalConfig.enabled) return false;
    if (isPremium) return false;

    // Respect auth gating: honor showForAnonymous / showForLoggedIn
    if (!isAuthenticated && !finalConfig.showForAnonymous) return false;
    if (isAuthenticated && !finalConfig.showForLoggedIn) return false;

    // Note: Bottom bar does NOT respect session dismissal (by design - "No Dismissal Penalty")
    // Bottom bar stays visible from firstShowPageCount onwards
    return pageCount.value >= finalConfig.firstShowPageCount;
  });

  const shouldShowOffcanvas = computed(() => isExpanded.value);

  const currentLevel = computed((): EngagementLevel | null => {
    if (!finalConfig.enabled || isPremium) return null;

    const level = calculateEngagementLevel(pageCount.value);
    const message = getMessage(level);

    return {
      level,
      message,
    };
  });

  /**
   * Determine if Google Ads should be shown
   * Ads show when: not on first page, enabled, not expanded, not dismissed for session
   */
  const shouldShowAds = computed(() => {
    // Check cookie consent
    const hasCookieConsent = () => {
      if (typeof document === "undefined") return false;
      return document.cookie.includes("cookieconsent_status=dismiss");
    };

    return (
      finalConfig.enabled &&
      pageCount.value > 1 &&
      !isExpanded.value &&
      !isDismissedForSession() &&
      hasCookieConsent()
    );
  });

  // Initialize on creation
  initialize();

  return {
    shouldShowBottomBar,
    shouldShowOffcanvas,
    isExpanded,
    currentLevel,
    shouldShowAds,
    expand,
    collapse,
    dismissForSession, // Optional, for future use
    // Expose for testing
    _getPageCount: getPageCount,
    _isDismissedForSession: isDismissedForSession,
  };
}
