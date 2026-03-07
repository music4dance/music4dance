/**
 * Engagement offcanvas composable
 * Determines when to show engagement prompts to anonymous users based on page load count
 */

import { ref, computed } from "vue";
import type { EngagementConfig } from "@/models/EngagementConfig";
import { defaultEngagementConfig } from "@/models/EngagementConfig";

const STORAGE_KEY_USAGE_COUNT = "usageCount";
const SESSION_KEY_DISMISSED = "engagementDismissed";

export interface EngagementLevel {
  level: 1 | 2 | 3;
  message: string;
  ctaUrls: {
    primary: string;
    secondary: string;
    tertiary: string;
  };
}

/**
 * Composable for managing engagement offcanvas display logic
 * @param config - Engagement configuration from server (optional, falls back to defaults)
 * @param isAuthenticated - Whether the user is authenticated (default: false)
 * @returns Object with reactive state and control methods
 *
 * @example
 * ```typescript
 * const engagement = useEngagementOffcanvas(menuContext.engagementConfig, false);
 *
 * // Check if offcanvas should show
 * if (engagement.shouldShowOffcanvas.value) {
 *   // Display offcanvas with engagement.currentLevel
 * }
 *
 * // User dismisses offcanvas
 * engagement.dismiss();
 * ```
 */
export function useEngagementOffcanvas(config?: EngagementConfig, isAuthenticated = false) {
  // Merge provided config with defaults
  const finalConfig = { ...defaultEngagementConfig, ...config };

  // Reactive state
  const isVisible = ref(false);
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
    // Never show if disabled, authenticated, or dismissed for session
    if (!finalConfig.enabled || isAuthenticated || isDismissedForSession()) {
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
   * Get message for current engagement level with {pageCount} placeholder replaced
   */
  function getMessage(level: 1 | 2 | 3, count: number): string {
    let message = "";
    if (level === 1) {
      message = finalConfig.messages.level1;
    } else if (level === 2) {
      message = finalConfig.messages.level2;
    } else {
      message = finalConfig.messages.level3;
    }

    // Replace {pageCount} placeholder if present
    return message.replace("{pageCount}", count.toString());
  }

  /**
   * Initialize: Read page count and determine if offcanvas should show
   */
  function initialize(): void {
    pageCount.value = getPageCount();
    isVisible.value = calculateShouldShow(pageCount.value);
  }

  /**
   * Dismiss offcanvas for the session
   */
  function dismiss(): void {
    isVisible.value = false;
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

  /**
   * Show offcanvas (e.g., for testing or forced display)
   */
  function show(): void {
    if (!isAuthenticated && finalConfig.enabled) {
      isVisible.value = true;
    }
  }

  // Computed properties
  const shouldShowOffcanvas = computed(() => isVisible.value);

  const currentLevel = computed((): EngagementLevel | null => {
    if (!isVisible.value) return null;

    const level = calculateEngagementLevel(pageCount.value);
    const message = getMessage(level, pageCount.value);

    return {
      level,
      message,
      ctaUrls: {
        primary: finalConfig.ctaUrls.register,
        secondary: finalConfig.ctaUrls.features,
        tertiary: finalConfig.ctaUrls.subscribe,
      },
    };
  });

  /**
   * Determine if Google Ads should be shown
   * Same rules as offcanvas: not on first page, enabled, not authenticated
   */
  const shouldShowAds = computed(() => {
    return (
      finalConfig.enabled && !isAuthenticated && pageCount.value > 0 && !isDismissedForSession()
    );
  });

  // Initialize on creation
  initialize();

  return {
    shouldShowOffcanvas,
    currentLevel,
    shouldShowAds,
    dismiss,
    show,
    // Expose for testing
    _getPageCount: getPageCount,
    _isDismissedForSession: isDismissedForSession,
  };
}
