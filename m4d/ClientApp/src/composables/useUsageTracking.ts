/**
 * Client-side usage tracking composable
 * Tracks page views and sends to server API with smart batching
 */

import type { MenuContextInterface } from '@/models/MenuContext';

interface UsageEvent {
  usageId: string;
  timestamp: number;
  page: string;
  query: string;
  referrer: string | null;
  userAgent: string;
  filter: string | null;
  userName: string | null;
  isAuthenticated: boolean;
}

interface UsageQueue {
  events: UsageEvent[];
  lastSentIndex: number;
}

interface UsageTrackerConfig {
  enabled: boolean;
  anonymousThreshold: number;
  anonymousBatchSize: number;
  authenticatedBatchSize: number;
  maxQueueSize: number;
  apiEndpoint: string;
  debug: boolean;
  menuContext?: MenuContextInterface;
  // Alternative to menuContext - pass individual properties
  xsrfToken?: string;
  userName?: string | null;
  isAuthenticated?: boolean;
}

const STORAGE_KEY_USAGE_ID = 'usageId';
const STORAGE_KEY_USAGE_QUEUE = 'usageQueue';
const STORAGE_KEY_USAGE_COUNT = 'usageCount';

/**
 * Client-side usage tracking composable
 * Tracks page views and sends batched events to server API with smart unload handling
 * 
 * @param config - Configuration options (all optional)
 * @returns Object with tracking methods: trackPageView, getUsageId, getVisitCount
 * 
 * @example
 * ```typescript
 * // Initialize with configuration
 * const tracker = useUsageTracking({
 *   anonymousThreshold: 3,
 *   anonymousBatchSize: 5,
 *   xsrfToken: 'token',
 *   isAuthenticated: false
 * });
 * 
 * // Track page view
 * tracker.trackPageView('/dances', '?filter=CHA');
 * 
 * // Get current visit count
 * const count = tracker.getVisitCount(); // Returns number of pages visited
 * ```
 * 
 * @remarks
 * - Anonymous users: Batch of 5 events sent after 3 page threshold
 * - Authenticated users: Immediate send (batch size 1)
 * - Uses Navigation API for smart unload handling (85-90% browser support)
 * - Graceful degradation for older browsers
 * - Bot detection via user agent and webdriver
 */
export function useUsageTracking(config: Partial<UsageTrackerConfig> = {}) {
const defaultConfig: UsageTrackerConfig = {
  enabled: true,
  anonymousThreshold: 3,
  anonymousBatchSize: 5,
  authenticatedBatchSize: 1,
  maxQueueSize: 100,
  apiEndpoint: '/api/usagelog/batch',
  debug: import.meta.env.DEV,
  menuContext: undefined,
};

const finalConfig = { ...defaultConfig, ...config };
const xsrfToken = finalConfig.xsrfToken || finalConfig.menuContext?.xsrfToken || '';

// Validate XSRF token in debug mode
if (finalConfig.debug) {
  if (!xsrfToken) {
    console.warn('Usage tracking: No XSRF token provided, API calls will fail');
  } else if (xsrfToken.length < 20) {
    console.warn('Usage tracking: XSRF token seems too short, may be invalid');
  }
}

if (!finalConfig.enabled) {
  return {
    trackPageView: () => {},
    getUsageId: () => null,
    getVisitCount: () => 0,
  };
}

  // Initialize or load UsageId
  function getUsageId(): string {
    let usageId = localStorage.getItem(STORAGE_KEY_USAGE_ID);
    if (!usageId) {
      usageId = crypto.randomUUID();
      localStorage.setItem(STORAGE_KEY_USAGE_ID, usageId);
    }
    return usageId;
  }

  // Get visit count
  function getVisitCount(): number {
    const count = localStorage.getItem(STORAGE_KEY_USAGE_COUNT);
    return count ? parseInt(count, 10) : 0;
  }

  // Increment visit count
  function incrementVisitCount(): number {
    const newCount = getVisitCount() + 1;
    localStorage.setItem(STORAGE_KEY_USAGE_COUNT, newCount.toString());
    return newCount;
  }

  // Load queue from localStorage
  function loadQueue(): UsageQueue {
    try {
      const stored = localStorage.getItem(STORAGE_KEY_USAGE_QUEUE);
      if (stored) {
        const parsed = JSON.parse(stored) as UsageQueue;
        return {
          events: parsed.events || [],
          lastSentIndex: parsed.lastSentIndex || -1,
        };
      }
    } catch (error) {
      if (finalConfig.debug) {
        console.error('Failed to load usage queue:', error);
      }
    }
    return { events: [], lastSentIndex: -1 };
  }

  // Save queue to localStorage
  function saveQueue(queue: UsageQueue): void {
    try {
      // Trim queue if too large
      if (queue.events.length > finalConfig.maxQueueSize) {
        const trimCount = queue.events.length - finalConfig.maxQueueSize;
        queue.events = queue.events.slice(trimCount);
        queue.lastSentIndex = Math.max(-1, queue.lastSentIndex - trimCount);
      }
      localStorage.setItem(STORAGE_KEY_USAGE_QUEUE, JSON.stringify(queue));
    } catch (error) {
      if (finalConfig.debug) {
        console.error('Failed to save usage queue:', error);
      }
    }
  }

  // Detect if user is authenticated
  function isAuthenticated(): boolean {
    // Check explicit config first
    if (finalConfig.isAuthenticated !== undefined) {
      return finalConfig.isAuthenticated;
    }
    // Check MenuContext (most reliable)
    if (finalConfig.menuContext?.userName) {
      return true;
    }
    // Fallback: check for authentication cookie
    return document.cookie.includes('.AspNetCore.Identity.Application');
  }

  // Get username if authenticated
  function getUserName(): string | null {
    // Get from explicit config first
    if (finalConfig.userName !== undefined) {
      return finalConfig.userName;
    }
    // Get from MenuContext (server-provided)
    return finalConfig.menuContext?.userName || null;
  }

  // Detect bot
  function isBot(): boolean {
    const ua = navigator.userAgent.toLowerCase();

    const botPatterns = [
      /bot/i,
      /crawl/i,
      /spider/i,
      /slurp/i,
      /headless/i,
      /phantom/i,
      /puppeteer/i,
      /selenium/i,
      /webdriver/i,
    ];

    if (botPatterns.some((pattern) => pattern.test(ua))) {
      return true;
    }

    // Check for headless Chrome/Firefox
    if ('__nightmare' in window || '__phantomas' in window) {
      return true;
    }

    // Check for webdriver
    if (navigator.webdriver === true) {
      return true;
    }

    return false;
  }

  // Send batch using sendBeacon with FormData
  function sendBatch(events: UsageEvent[]): boolean {
    if (events.length === 0) {
      return true;
    }

    // Create FormData with JSON payload and XSRF token
    const formData = new FormData();
    formData.append('events', JSON.stringify(events));
    formData.append('__RequestVerificationToken', xsrfToken);

    if (finalConfig.debug) {
      console.log('SendBeacon request:', {
        endpoint: finalConfig.apiEndpoint,
        eventsCount: events.length,
        hasToken: !!xsrfToken,
        tokenLength: xsrfToken?.length,
        tokenPreview: xsrfToken?.substring(0, 20) + '...'
      });
    }

    try {
      const success = navigator.sendBeacon(finalConfig.apiEndpoint, formData);
      if (finalConfig.debug) {
        console.log(
          `SendBeacon: ${success ? 'Accepted' : 'Rejected'} ${events.length} events`
        );
      }
      return success;
    } catch (error) {
      if (finalConfig.debug) {
        console.error('SendBeacon failed:', error);
      }
      return false;
    }
  }

  // Track page view
  function trackPageView(
    page: string = window.location.pathname,
    query: string = window.location.search
  ): void {
    if (isBot()) {
      if (finalConfig.debug) {
        console.log('Usage tracking: Bot detected, skipping');
      }
      return;
    }

    const usageId = getUsageId();
    const visitCount = incrementVisitCount();
    const authenticated = isAuthenticated();
    const userName = authenticated ? getUserName() : null;

    // Extract filter from query string
    const urlParams = new URLSearchParams(query);
    const filter = urlParams.get('filter');

    // Create event
    const event: UsageEvent = {
      usageId,
      timestamp: Date.now(),
      page,
      query,
      referrer: document.referrer || null,
      userAgent: navigator.userAgent,
      filter,
      userName,
      isAuthenticated: authenticated,
    };

    // Load queue and add event
    const queue = loadQueue();
    queue.events.push(event);
    saveQueue(queue);

    if (finalConfig.debug) {
      console.log('Usage tracking: Event queued', {
        page,
        authenticated,
        visitCount,
        queueLength: queue.events.length,
        lastSentIndex: queue.lastSentIndex,
      });
    }

    // Determine if we should send
    const unsentEvents = queue.events.slice(queue.lastSentIndex + 1);
    const threshold = authenticated ? 1 : finalConfig.anonymousThreshold;
    const batchSize = authenticated
      ? finalConfig.authenticatedBatchSize
      : finalConfig.anonymousBatchSize;

    if (finalConfig.debug) {
      console.log('Usage tracking: Send check', {
        visitCount,
        threshold,
        unsentEventsCount: unsentEvents.length,
        batchSize,
        willSend: visitCount >= threshold && unsentEvents.length >= batchSize,
      });
    }

    // Check if we should send
    if (visitCount >= threshold && unsentEvents.length >= batchSize) {
      const eventsToSend = unsentEvents.slice(0, batchSize);

      // Send synchronously using sendBeacon
      const success = sendBatch(eventsToSend);
      
      if (success) {
        // Update lastSentIndex
        const newQueue = loadQueue();
        newQueue.lastSentIndex += eventsToSend.length;
        saveQueue(newQueue);
        
        if (finalConfig.debug) {
          console.log('Usage tracking: Batch sent', {
            eventsCount: eventsToSend.length,
            newLastSentIndex: newQueue.lastSentIndex,
          });
        }
      }
    }
  }

  // Register page unload handler with Navigation API support
  // Uses Navigation API to detect same-origin navigations and skip sending,
  // while still sending for external navigation or tab close
  function registerUnloadHandler(): void {
    let isInternalNavigation = false;

    // Try to use Navigation API (modern browsers)
    if ('navigation' in window && (window as any).navigation) {
      try {
        (window as any).navigation.addEventListener('navigate', (event: any) => {
          // Check if destination is same-origin
          const destinationUrl = event.destination?.url;
          if (destinationUrl) {
            try {
              const destination = new URL(destinationUrl);
              const isSameOrigin = destination.origin === window.location.origin;
              
              if (isSameOrigin) {
                // Internal navigation - set flag to skip unload send
                isInternalNavigation = true;
                
                // Clear flag after navigation completes (or timeout as fallback)
                setTimeout(() => {
                  isInternalNavigation = false;
                }, 100);
                
                if (finalConfig.debug) {
                  console.log('Usage tracking: Internal navigation detected, will skip unload send');
                }
              }
            } catch (e) {
              // Invalid URL, treat as external
              if (finalConfig.debug) {
                console.warn('Usage tracking: Could not parse destination URL:', e);
              }
            }
          }
        });
        
        if (finalConfig.debug) {
          console.log('Usage tracking: Navigation API enabled for smart unload handling');
        }
      } catch (e) {
        if (finalConfig.debug) {
          console.warn('Usage tracking: Navigation API setup failed, using fallback:', e);
        }
      }
    }

    const handleUnload = () => {
      // Skip send if we detected an internal navigation
      if (isInternalNavigation) {
        if (finalConfig.debug) {
          console.log('Usage tracking: Skipping unload send (internal navigation)');
        }
        return;
      }

      const queue = loadQueue();
      const unsentEvents = queue.events.slice(queue.lastSentIndex + 1);
      const visitCount = getVisitCount();
      const authenticated = isAuthenticated();
      const threshold = authenticated ? 1 : finalConfig.anonymousThreshold;

      // Only send if visit count >= threshold
      if (visitCount >= threshold && unsentEvents.length > 0) {
        if (finalConfig.debug) {
          console.log('Usage tracking: Sending unsent events on unload', {
            eventCount: unsentEvents.length,
            reason: 'external navigation or tab close'
          });
        }
        
        const success = sendBatch(unsentEvents);
        if (success) {
          // Update lastSentIndex (optimistic)
          queue.lastSentIndex = queue.events.length - 1;
          saveQueue(queue);
        }
      }
    };

    // Primary: visibilitychange (most reliable)
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'hidden') {
        handleUnload();
      }
    });

    // Fallback: pagehide (for iOS Safari)
    window.addEventListener('pagehide', handleUnload);
  }

  // Initialize with smart unload handling
  registerUnloadHandler();

  return {
    trackPageView,
    getUsageId,
    getVisitCount,
  };
}
