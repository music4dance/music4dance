import { beforeAll } from "vitest";
import { mockHealthFetch } from "@/helpers/TestHelpers";

// Mock fetch globally for all tests to handle health API calls
beforeAll(() => {
  mockHealthFetch();
});
