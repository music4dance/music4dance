import { MenuContext } from "@/models/MenuContext";
import { loadTestDances } from "./LoadTestDances";
import tagDatabaseJson from "@/assets/content/tags.json";
import type { ServiceHealthResponse } from "@/composables/useServiceHealth";

declare global {
  interface Window {
    danceDatabaseJson?: string;
    tagDatabaseJson?: string;
  }
}

/**
 * Mock health response for tests
 */
const mockHealthResponse: ServiceHealthResponse = {
  timestamp: new Date().toISOString(),
  overallStatus: "healthy",
  summary: {
    healthy: 3,
    degraded: 0,
    unavailable: 0,
    unknown: 0,
  },
  services: [
    {
      name: "Database",
      status: "healthy",
      lastChecked: new Date().toISOString(),
      consecutiveFailures: 0,
    },
    {
      name: "SearchService",
      status: "healthy",
      lastChecked: new Date().toISOString(),
      consecutiveFailures: 0,
    },
    {
      name: "AppConfiguration",
      status: "healthy",
      lastChecked: new Date().toISOString(),
      consecutiveFailures: 0,
    },
  ],
};

/**
 * Mock global fetch for health API endpoint
 */
export const mockHealthFetch = () => {
  global.fetch = async (url: string | URL | Request) => {
    const urlString =
      typeof url === "string" ? url : url instanceof Request ? url.url : url.toString();

    if (urlString.includes("/api/health/status")) {
      return {
        ok: true,
        status: 200,
        json: async () => mockHealthResponse,
      } as Response;
    }

    // For other URLs, return empty response
    return {
      ok: false,
      status: 404,
      json: async () => ({}),
    } as Response;
  };
};

export const setupTestEnvironment = () => {
  window.danceDatabaseJson = loadTestDances();
  window.tagDatabaseJson = JSON.stringify(tagDatabaseJson);
  mockHealthFetch();
};

export const mockResizObserver = () => {
  window.ResizeObserver = class ResizeObserver {
    observe() {
      // do nothing
    }
    unobserve() {
      // do nothing
    }
    disconnect() {
      // do nothing
    }
  };
};

export function m4dContext(): MenuContext {
  return new MenuContext({
    helpLink: "https://music4dance.blog/music4dance-help/song/",
    userName: "music4dance",
    userId: "",
    roles: ["showDiagnostics", "canEdit", "dbAdmin", "canTag", "premium"],
    indexId: "c",
    xsrfToken: "FOO",
  });
}
