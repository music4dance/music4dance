import { fileURLToPath } from "node:url";
import { mergeConfig, defineConfig, configDefaults } from "vitest/config";
import viteConfig from "./vite.config";

// vite-plugin-mkcert's internal plugin name (vite-plugin-mkcert -> vite:plugin:mkcert).
const MKCERT_PLUGIN_NAME = "vite:plugin:mkcert";

export default mergeConfig(
  {
    ...viteConfig,
    // Vitest never starts a real HTTPS dev server, so mkcert's certificate generation is
    // unneeded here - and it's fragile in CI: its config hook calls the GitHub API to fetch
    // the mkcert binary's release info, which GitHub Actions runners routinely get 403'd on
    // due to shared-IP rate limiting. Locally it's silent because the binary is already
    // cached under ~/.vite-plugin-mkcert from prior dev-server runs.
    plugins: (viteConfig.plugins ?? []).filter(
      (plugin) =>
        !(
          plugin &&
          typeof plugin === "object" &&
          "name" in plugin &&
          plugin.name === MKCERT_PLUGIN_NAME
        ),
    ),
  },
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
