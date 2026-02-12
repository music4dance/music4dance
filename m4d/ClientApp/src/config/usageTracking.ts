/**
 * Usage tracking configuration
 * Configuration values come from server-side MenuContext.usageTracking
 * set in Razor Pages _head.cshtml from appsettings.json
 */

import type { MenuContextInterface } from '@/models/MenuContext';

export interface UsageTrackingServerConfig {
  enabled: boolean;
  anonymousThreshold: number;
  anonymousBatchSize: number;
  authenticatedBatchSize: number;
  maxQueueSize: number;
}

// Get MenuContext from window (set by Razor Page)
const menuContext = (window as any).menuContext as MenuContextInterface | undefined;

export const usageTrackingConfig = {
  enabled: menuContext?.usageTracking?.enabled ?? true,
  anonymousThreshold: menuContext?.usageTracking?.anonymousThreshold ?? 3,
  anonymousBatchSize: menuContext?.usageTracking?.anonymousBatchSize ?? 5,
  authenticatedBatchSize: menuContext?.usageTracking?.authenticatedBatchSize ?? 1,
  maxQueueSize: menuContext?.usageTracking?.maxQueueSize ?? 100,
  apiEndpoint: '/api/usagelog/batch',
  debug: import.meta.env.DEV,
};


