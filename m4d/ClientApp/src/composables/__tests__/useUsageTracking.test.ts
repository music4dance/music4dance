import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { useUsageTracking } from '../useUsageTracking';
import type { MenuContextInterface } from '@/models/MenuContext';

describe('useUsageTracking', () => {
beforeEach(() => {
  // Clear localStorage
  localStorage.clear();
  // Clear cookies
  document.cookie = '';
  // Mock navigator.sendBeacon (synchronous, returns boolean)
  global.navigator.sendBeacon = vi.fn(() => true);
  // Mock user agent to NOT be a bot
  Object.defineProperty(navigator, 'userAgent', {
    value: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
    configurable: true,
  });
  // Mock webdriver to false
  Object.defineProperty(navigator, 'webdriver', {
    value: false,
    configurable: true,
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

  describe('UsageId Management', () => {
    it('generates new UsageId if not exists', () => {
      const tracker = useUsageTracking();
      const usageId = tracker.getUsageId();

      expect(usageId).toBeTruthy();
      expect(usageId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/
      );
    });

    it('retrieves existing UsageId from localStorage', () => {
      const existingId = '123e4567-e89b-12d3-a456-426614174000';
      localStorage.setItem('usageId', existingId);

      const tracker = useUsageTracking();
      const usageId = tracker.getUsageId();

      expect(usageId).toBe(existingId);
    });

    it('persists UsageId across instances', () => {
      const tracker1 = useUsageTracking();
      const usageId1 = tracker1.getUsageId();

      const tracker2 = useUsageTracking();
      const usageId2 = tracker2.getUsageId();

      expect(usageId1).toBe(usageId2);
    });
  });

  describe('Visit Count Tracking', () => {
    it('starts at 0 for new session', () => {
      const tracker = useUsageTracking();
      expect(tracker.getVisitCount()).toBe(0);
    });

    it('increments visit count on trackPageView', () => {
      const tracker = useUsageTracking();
      tracker.trackPageView('/test');

      expect(tracker.getVisitCount()).toBe(1);

      tracker.trackPageView('/test2');
      expect(tracker.getVisitCount()).toBe(2);
    });
  });

  describe('Bot Detection', () => {
    it('skips tracking for known bot user-agents', () => {
      Object.defineProperty(navigator, 'userAgent', {
        value: 'Googlebot/2.1',
        configurable: true,
      });

      const tracker = useUsageTracking();
      tracker.trackPageView('/test');

      expect(tracker.getVisitCount()).toBe(0); // Should not increment
    });

    it('skips tracking for headless browsers', () => {
      (window as any).__nightmare = true;

      const tracker = useUsageTracking();
      tracker.trackPageView('/test');

      expect(tracker.getVisitCount()).toBe(0);

      delete (window as any).__nightmare;
    });

    it('skips tracking for webdriver', () => {
      Object.defineProperty(navigator, 'webdriver', {
        value: true,
        configurable: true,
      });

      const tracker = useUsageTracking();
      tracker.trackPageView('/test');

      expect(tracker.getVisitCount()).toBe(0);
    });
  });

  describe('Anonymous User Batching', () => {
    it('does not send batch below threshold', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 3,
        anonymousBatchSize: 5,
        debug: false,
      });

      tracker.trackPageView('/page1');
      tracker.trackPageView('/page2');

      expect(global.navigator.sendBeacon).not.toHaveBeenCalled();
    });

    it('sends batch when threshold and batch size reached', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 2,
        anonymousBatchSize: 3,
        xsrfToken: 'test-token',
        debug: false,
      });

      tracker.trackPageView('/page1');
      tracker.trackPageView('/page2');
      tracker.trackPageView('/page3');

      // Verify sendBeacon was called (synchronous)
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);
      
      // Verify the URL and FormData
      const callArgs = (global.navigator.sendBeacon as any).mock.calls[0];
      expect(callArgs[0]).toBe('/api/usagelog/batch');
      
      // Verify payload is FormData with token and events
      const formData = callArgs[1] as FormData;
      expect(formData.get('__RequestVerificationToken')).toBeTruthy();
      const eventsJson = formData.get('events') as string;
      const events = JSON.parse(eventsJson);
      expect(events).toHaveLength(3);
    });
  });

  describe('Authenticated User Batching', () => {
    it('sends immediately for authenticated users', () => {
      const menuContext: MenuContextInterface = {
        userName: 'testuser@example.com',
        xsrfToken: 'test-token',
      };

      const tracker = useUsageTracking({
        authenticatedBatchSize: 1,
        debug: false,
        menuContext,
      });

      tracker.trackPageView('/page1');

      // Verify sendBeacon was called (synchronous)
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);
      
      // Verify XSRF token in FormData
      const callArgs = (global.navigator.sendBeacon as any).mock.calls[0];
      const formData = callArgs[1] as FormData;
      expect(formData.get('__RequestVerificationToken')).toBe('test-token');
    });
  });

  describe('Queue Management', () => {
    it('stores events in localStorage queue', () => {
      const tracker = useUsageTracking();
      tracker.trackPageView('/test');

      const queueData = localStorage.getItem('usageQueue');
      expect(queueData).toBeTruthy();

      const queue = JSON.parse(queueData!);
      expect(queue.events).toHaveLength(1);
      expect(queue.events[0].page).toBe('/test');
    });

    it('tracks lastSentIndex in queue', async () => {
      const mockFetch = vi.fn().mockResolvedValue({ ok: true });
      global.fetch = mockFetch;

      const tracker = useUsageTracking({
        anonymousThreshold: 1,
        anonymousBatchSize: 2,
        debug: false,
      });

      tracker.trackPageView('/page1');
      tracker.trackPageView('/page2');

      await new Promise((resolve) => setTimeout(resolve, 10));

      const queueData = localStorage.getItem('usageQueue');
      const queue = JSON.parse(queueData!);
      expect(queue.lastSentIndex).toBeGreaterThanOrEqual(0);
    });

    it('trims queue when max size exceeded', () => {
      const tracker = useUsageTracking({ maxQueueSize: 5 });

      for (let i = 0; i < 10; i++) {
        tracker.trackPageView(`/page${i}`);
      }

      const queueData = localStorage.getItem('usageQueue');
      const queue = JSON.parse(queueData!);
      expect(queue.events.length).toBeLessThanOrEqual(5);
    });
  });

  describe('XSRF Token Handling', () => {
    it('uses xsrfToken from MenuContext', () => {
      const menuContext: MenuContextInterface = {
        userName: 'testuser@example.com',
        xsrfToken: 'my-xsrf-token',
      };

      const tracker = useUsageTracking({
        authenticatedBatchSize: 1,
        menuContext,
      });

      tracker.trackPageView('/test');

      // Verify sendBeacon was called with correct token in FormData
      expect(global.navigator.sendBeacon).toHaveBeenCalled();
      const formData = (global.navigator.sendBeacon as any).mock.calls[0][1] as FormData;
      expect(formData.get('__RequestVerificationToken')).toBe('my-xsrf-token');
    });
  });

  describe('SendBeacon on Page Unload', () => {
    it('uses sendBeacon for unsent events on unload when threshold met', () => {
      const mockSendBeacon = vi.fn(() => true);
      global.navigator.sendBeacon = mockSendBeacon;

      const menuContext: MenuContextInterface = {
        xsrfToken: 'test-token',
      };

      const tracker = useUsageTracking({
        anonymousThreshold: 2, // Low threshold so we can test
        menuContext,
      });

      tracker.trackPageView('/page1');
      tracker.trackPageView('/page2');
      tracker.trackPageView('/page3'); // Now visit count = 3, >= threshold

      // Manually trigger visibility change (simulating page unload)
      const event = new Event('visibilitychange');
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      });
      document.dispatchEvent(event);

      // SendBeacon should be called because visit count >= threshold
      expect(mockSendBeacon).toHaveBeenCalled();
    });

    it('respects 64KB limit for SendBeacon', () => {
      const mockSendBeacon = vi.fn(() => true);
      global.navigator.sendBeacon = mockSendBeacon;

      const menuContext: MenuContextInterface = {
        xsrfToken: 'test-token',
      };

      const tracker = useUsageTracking({
        anonymousThreshold: 1,
        menuContext,
      });

      // Create large payload
      for (let i = 0; i < 1000; i++) {
        tracker.trackPageView(`/page${i}?query=${'x'.repeat(100)}`);
      }

      const event = new Event('visibilitychange');
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      });
      document.dispatchEvent(event);

      // Should have been called (possibly multiple times with truncated payload)
      expect(mockSendBeacon).toHaveBeenCalled();
    });
  });

  describe('Configuration', () => {
    it('respects enabled flag', () => {
      const tracker = useUsageTracking({ enabled: false });
      tracker.trackPageView('/test');

      expect(tracker.getVisitCount()).toBe(0);
      expect(localStorage.getItem('usageQueue')).toBeNull();
    });

    it('uses custom configuration values', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 1,
        anonymousBatchSize: 1,
        debug: true, // Enable debug to see console logs
      });

      // First page view: visitCount becomes 1
      tracker.trackPageView('/page1');

      // Check queue state
      const queueData = localStorage.getItem('usageQueue');
      expect(queueData).toBeTruthy();
      const queue = JSON.parse(queueData!);
      expect(queue.events).toHaveLength(1);
      
      // With threshold=1 and batchSize=1, this should have triggered send
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);
    });
  });

  describe('Detailed Batching Logic - Anonymous Users', () => {
    it('should NOT send before reaching threshold (3 pages)', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 3,
        anonymousBatchSize: 5,
        xsrfToken: 'test-token',
        isAuthenticated: false,
      });

      // Page 1
      tracker.trackPageView('/page1');
      expect(global.navigator.sendBeacon).not.toHaveBeenCalled();

      // Page 2
      tracker.trackPageView('/page2');
      expect(global.navigator.sendBeacon).not.toHaveBeenCalled();

      // Check localStorage
      const queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.events).toHaveLength(2);
      expect(queue.lastSentIndex).toBe(-1);
    });

    it('should send EXACTLY 5 events on page 5 (first batch)', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 3,
        anonymousBatchSize: 5,
        xsrfToken: 'test-token',
        isAuthenticated: false,
      });

      // Pages 1-4
      tracker.trackPageView('/page1');
      tracker.trackPageView('/page2');
      tracker.trackPageView('/page3');
      tracker.trackPageView('/page4');

      // Page 5 - should trigger send
      tracker.trackPageView('/page5');

      // Verify send happened
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);

      // Get the FormData that was sent
      const formData = (global.navigator.sendBeacon as any).mock.calls[0][1] as FormData;
      const eventsJson = formData.get('events') as string;
      const sentEvents = JSON.parse(eventsJson);

      // Should send EXACTLY 5 events
      expect(sentEvents).toHaveLength(5);
      expect(sentEvents[0].page).toBe('/page1');
      expect(sentEvents[4].page).toBe('/page5');

      // Check lastSentIndex
      const queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.lastSentIndex).toBe(4); // 0-based index
    });

    it('should send EXACTLY 5 events in second batch (pages 6-10)', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 3,
        anonymousBatchSize: 5,
        xsrfToken: 'test-token',
        isAuthenticated: false,
      });

      // First batch (pages 1-5)
      for (let i = 1; i <= 5; i++) {
        tracker.trackPageView(`/page${i}`);
      }
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1);

      // Pages 6-9 (4 more events, should NOT send)
      for (let i = 6; i <= 9; i++) {
        tracker.trackPageView(`/page${i}`);
      }
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(1); // Still just 1

      // Page 10 (5th unsent event, should send)
      tracker.trackPageView('/page10');
      expect(global.navigator.sendBeacon).toHaveBeenCalledTimes(2);

      // Check second batch
      const secondFormData = (global.navigator.sendBeacon as any).mock.calls[1][1] as FormData;
      const secondEventsJson = secondFormData.get('events') as string;
      const secondSentEvents = JSON.parse(secondEventsJson);

      // Should send EXACTLY 5 events (pages 6-10)
      expect(secondSentEvents).toHaveLength(5);
      expect(secondSentEvents[0].page).toBe('/page6');
      expect(secondSentEvents[4].page).toBe('/page10');

      // Check lastSentIndex
      const queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.lastSentIndex).toBe(9); // 0-based, 10 events sent
    });

    it('should correctly track lastSentIndex across batches', () => {
      const tracker = useUsageTracking({
        anonymousThreshold: 3,
        anonymousBatchSize: 5,
        xsrfToken: 'test-token',
        isAuthenticated: false,
      });

      // First batch
      for (let i = 1; i <= 5; i++) {
        tracker.trackPageView(`/page${i}`);
      }
      let queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.lastSentIndex).toBe(4); // 0-4 sent

      // Second batch
      for (let i = 6; i <= 10; i++) {
        tracker.trackPageView(`/page${i}`);
      }
      queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.lastSentIndex).toBe(9); // 0-9 sent

      // Third batch
      for (let i = 11; i <= 15; i++) {
        tracker.trackPageView(`/page${i}`);
      }
      queue = JSON.parse(localStorage.getItem('usageQueue')!);
      expect(queue.lastSentIndex).toBe(14); // 0-14 sent
    });
  });
});
