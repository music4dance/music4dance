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
}

const STORAGE_KEY_USAGE_ID = 'usageId';
const STORAGE_KEY_USAGE_QUEUE = 'usageQueue';
const STORAGE_KEY_USAGE_COUNT = 'usageCount';

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
const xsrfToken = finalConfig.menuContext?.xsrfToken || '';

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
    // Check MenuContext first (most reliable)
    if (finalConfig.menuContext?.userName) {
      return true;
    }
    // Fallback: check for authentication cookie
    return document.cookie.includes('.AspNetCore.Identity.Application');
  }

  // Get username if authenticated
  function getUserName(): string | null {
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

  // Send batch using sendBeacon
  function sendBatch(events: UsageEvent[]): boolean {
    if (events.length === 0) {
      return true;
    }

    const payload = JSON.stringify({ events });

    // Check 64KB limit
    const sizeInBytes = new Blob([payload]).size;
    if (sizeInBytes > 65536) {
      if (finalConfig.debug) {
        console.warn(
          `SendBeacon payload too large: ${sizeInBytes} bytes. Truncating...`
        );
      }
      // Truncate to fit
      const maxEvents = Math.floor((events.length * 65536) / sizeInBytes);
      return sendBatch(events.slice(0, maxEvents));
    }

    // sendBeacon doesn't support custom headers, append token to URL
    const urlWithToken = `${finalConfig.apiEndpoint}?__RequestVerificationToken=${encodeURIComponent(xsrfToken)}`;

    try {
      const success = navigator.sendBeacon(urlWithToken, payload);
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
      }
    }
  }

  // Register page unload handler
  function registerUnloadHandler(): void {
    const handleUnload = () => {
      const queue = loadQueue();
      const unsentEvents = queue.events.slice(queue.lastSentIndex + 1);
      const visitCount = getVisitCount();
      const authenticated = isAuthenticated();
      const threshold = authenticated ? 1 : finalConfig.anonymousThreshold;

      // Only send if visit count >= threshold
      if (visitCount >= threshold && unsentEvents.length > 0) {
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

  // Initialize
  registerUnloadHandler();

  return {
    trackPageView,
    getUsageId,
    getVisitCount,
  };
}
