import { ref, onMounted, onUnmounted, computed } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";

export interface ServiceHealthStatus {
  name: string;
  status: "healthy" | "degraded" | "unavailable" | "unknown";
  lastChecked: string;
  lastHealthy?: string;
  errorMessage?: string;
  responseTime?: number;
  consecutiveFailures: number;
}

export interface ServiceHealthResponse {
  timestamp: string;
  overallStatus: "healthy" | "degraded" | "unavailable";
  updateMessage?: string; // Manual update warning from admin
  summary: {
    healthy: number;
    degraded: number;
    unavailable: number;
    unknown: number;
  };
  services: ServiceHealthStatus[];
}

/**
 * Create initial health response from MenuContext
 * This provides immediate health status on page load before first poll
 */
function createInitialHealthData(): ServiceHealthResponse {
  const menuContext = getMenuContext();
  const now = new Date().toISOString();

  const services: ServiceHealthStatus[] = [];

  // Add search service status
  if (menuContext.searchHealthy !== undefined) {
    services.push({
      name: "SearchService",
      status: menuContext.searchHealthy ? "healthy" : "unavailable",
      lastChecked: now,
      consecutiveFailures: menuContext.searchHealthy ? 0 : 1,
    });
  }

  // Add database status
  if (menuContext.databaseHealthy !== undefined) {
    services.push({
      name: "Database",
      status: menuContext.databaseHealthy ? "healthy" : "unavailable",
      lastChecked: now,
      consecutiveFailures: menuContext.databaseHealthy ? 0 : 1,
    });
  }

  // Add configuration status
  if (menuContext.configurationHealthy !== undefined) {
    services.push({
      name: "AppConfiguration",
      status: menuContext.configurationHealthy ? "healthy" : "unavailable",
      lastChecked: now,
      consecutiveFailures: menuContext.configurationHealthy ? 0 : 1,
    });
  }

  const healthyCount = services.filter((s) => s.status === "healthy").length;
  const unavailableCount = services.filter((s) => s.status === "unavailable").length;
  const degradedCount = services.filter((s) => s.status === "degraded").length;

  const overallStatus =
    unavailableCount > 0 ? "unavailable" : degradedCount > 0 ? "degraded" : "healthy";

  return {
    timestamp: now,
    overallStatus,
    summary: {
      healthy: healthyCount,
      degraded: degradedCount,
      unavailable: unavailableCount,
      unknown: 0,
    },
    services,
  };
}

/**
 * Composable for monitoring service health status
 * Polls /api/health/status endpoint and provides reactive health data
 * Initializes from MenuContext for immediate display on page load
 *
 * Uses adaptive polling intervals:
 * - When healthy: polls every hour (3600000ms) to reduce server load
 * - When degraded/unavailable or showing update message: polls every 30 seconds to detect recovery
 */
export function useServiceHealth() {
  // Initialize with MenuContext data for immediate display
  const healthData = ref<ServiceHealthResponse | null>(createInitialHealthData());
  const loading = ref(false);
  const error = ref<string | null>(null);
  let intervalId: number | null = null;

  // Adaptive polling intervals
  const HEALTHY_POLL_INTERVAL = 60 * 60 * 1000; // 1 hour when everything is healthy
  const UNHEALTHY_POLL_INTERVAL = 30 * 1000; // 30 seconds when showing warnings/errors

  const isServiceHealthy = (serviceName: string): boolean => {
    const service = healthData.value?.services.find((s) => s.name === serviceName);
    return service?.status === "healthy";
  };

  const isServiceDegraded = (serviceName: string): boolean => {
    const service = healthData.value?.services.find((s) => s.name === serviceName);
    return service?.status === "degraded";
  };

  const isServiceUnavailable = (serviceName: string): boolean => {
    const service = healthData.value?.services.find((s) => s.name === serviceName);
    return service?.status === "unavailable";
  };

  const getServiceStatus = (serviceName: string): ServiceHealthStatus | undefined => {
    return healthData.value?.services.find((s) => s.name === serviceName);
  };

  const hasAnyDegradedServices = computed(() => {
    return healthData.value?.overallStatus !== "healthy";
  });

  const degradedServiceCount = computed(() => {
    return (healthData.value?.summary.degraded || 0) + (healthData.value?.summary.unavailable || 0);
  });

  const fetchHealthStatus = async () => {
    try {
      loading.value = true;
      error.value = null;

      const response = await fetch("/api/health/status");

      // Parse JSON even for 503 responses - they contain health status
      if (response.ok || response.status === 503) {
        healthData.value = await response.json();
      } else {
        throw new Error(`Health check failed: ${response.status}`);
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : "Failed to fetch health status";
      console.error("Service health check failed:", err);
    } finally {
      loading.value = false;
    }
  };

  const getCurrentPollInterval = (): number => {
    // Use faster polling if services are unhealthy or if there's an update message
    const hasIssues =
      healthData.value?.overallStatus !== "healthy" ||
      (healthData.value?.updateMessage?.trim().length ?? 0) > 0;
    return hasIssues ? UNHEALTHY_POLL_INTERVAL : HEALTHY_POLL_INTERVAL;
  };

  const startPolling = () => {
    if (intervalId !== null) return;

    // Initial fetch
    fetchHealthStatus();

    // Set up adaptive polling that adjusts interval based on health status
    const poll = () => {
      fetchHealthStatus().then(() => {
        // Schedule next poll with appropriate interval based on current health
        intervalId = window.setTimeout(poll, getCurrentPollInterval());
      });
    };

    // Start first poll cycle
    intervalId = window.setTimeout(poll, getCurrentPollInterval());
  };

  const stopPolling = () => {
    if (intervalId !== null) {
      clearInterval(intervalId);
      intervalId = null;
    }
  };

  onMounted(() => {
    startPolling();
  });

  onUnmounted(() => {
    stopPolling();
  });

  return {
    healthData,
    loading,
    error,
    isServiceHealthy,
    isServiceDegraded,
    isServiceUnavailable,
    getServiceStatus,
    hasAnyDegradedServices,
    degradedServiceCount,
    fetchHealthStatus,
    startPolling,
    stopPolling,
  };
}
