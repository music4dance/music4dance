import { fileURLToPath } from "node:url";
import { mergeConfig, defineConfig, configDefaults } from "vitest/config";
import viteConfig from "./vite.config";

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: "jsdom",
      exclude: [...configDefaults.exclude, "e2e/*"],
      root: fileURLToPath(new URL("./", import.meta.url)),
      setupFiles: ["./vitest.setup.ts"],
      typecheck: {
        checker: "tsc",
      },
      // Increase timeouts for long-running baseline tests
      testTimeout: 60000, // 60 seconds per test (increased from default 5 seconds)
      hookTimeout: 30000, // 30 seconds for setup/teardown hooks
      teardownTimeout: 30000, // 30 seconds for cleanup
      // Worker timeout settings to prevent "onTaskUpdate" timeout errors
      pool: "threads",
      poolOptions: {
        threads: {
          // Increase worker timeout and memory limits
          minThreads: 1,
          maxThreads: 3,
          useAtomics: true,
        },
      },
      // Increase worker communication settings
      chaiConfig: {
        truncateThreshold: 10000,
      },
      // Reduce parallelism to avoid overwhelming the system with long tests
      maxConcurrency: 3,
      // Increase worker communication timeouts
      slowTestThreshold: 30000, // 30 seconds before marking as slow
      // Reporter settings to handle long test durations
      reporters: process.env.CI ? ["default", "junit"] : ["default"],
    },
  }),
);
