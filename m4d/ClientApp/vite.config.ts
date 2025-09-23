import { fileURLToPath, URL } from "node:url";

import { defineConfig, normalizePath, Plugin, UserConfig } from "vite";
import { dirname, resolve } from "path";
import vue from "@vitejs/plugin-vue";
import mkcert from "vite-plugin-mkcert";
import Components from "unplugin-vue-components/vite";
import { BootstrapVueNextResolver } from "bootstrap-vue-next";
import Inspect from "vite-plugin-inspect";
import fg from "fast-glob";
import Icons from "unplugin-icons/vite";
import IconsResolver from "unplugin-icons/resolver";

import appsettingsDev from "../appsettings.Development.json";

interface VuePlugin {
  name: string;
  library: string;
  config?: unknown;
}

type VuePluginMap = Record<string, [VuePlugin]>;

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    Inspect({ build: true, outputDir: ".vite-inspect" }),
    vue(),
    mkcert(),
    AutoEndpoints({
      "dance-details": [
        {
          name: "VueShowdownPlugin",
          library: "vue-showdown",
          config: { flavor: "vanilla" },
        },
      ],
    }),
    Components({
      globs: ["src/**/components/**/*.vue"],
      resolvers: [BootstrapVueNextResolver(), IconsResolver()],
      dts: true,
    }),
    Icons({ compiler: "vue3", autoInstall: true }),
  ],
  base: "/vclient",
  server: {
    port: appsettingsDev.Vite.Server.Port,
    strictPort: true,
    hmr: {
      clientPort: appsettingsDev.Vite.Server.Port,
    },
  },
  build: {
    outDir: "../wwwroot/vclient",
    emptyOutDir: true,
    manifest: true,
    cssCodeSplit: false,
    rollupOptions: {
      output: {
        experimentalMinChunkSize: 2048,
        manualChunks: {
          vue: ["vue"],
          showdown: ["vue-showdown"],
          bsvn: ["bootstrap-vue-next"],
        },
      },
    },
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  css: {
    preprocessorOptions: {
      scss: {
        silenceDeprecations: ["color-functions", "global-builtin", "import"],
      },
    },
  },
});

// This plugin will find all files matching the pattern src/pages/*/App.vue
//  and make endpoints with the 'name' being the replacement for the *. and the
//  path being src/pages/*/main.ts - then return a template for the main.ts file
function AutoEndpoints(config: VuePluginMap): Plugin {
  const main = `import 'vite/modulepreload-polyfill'

import { createApp, h } from 'vue'
import { createBootstrap } from 'bootstrap-vue-next'
{imports}
import Application from '{app}'
import {BApp} from 'bootstrap-vue-next'

import '@/scss/styles.scss'
import 'bootstrap-vue-next/dist/bootstrap-vue-next.css'

const Wrapper = {
  name: 'AppWrapper',
  render() {
    return h(BApp, null, { default: () => h(Application) });
  }
};

const app = createApp(Wrapper);

{configs}
app.mount('#app')
`;

  return {
    name: "auto-endpoints",
    async config(): Promise<UserConfig> {
      const root = "src/pages/";
      const pattern = root + "*/App.vue";
      const length = root.length;
      const dirs = (await fg.glob(pattern)).map((p) => dirname(p).substring(length));
      console.log(dirs.join(","));

      const input = dirs.reduce(
        (obj, item) => {
          const value = resolve(__dirname, root + item + "/main.ts");
          obj[item] = value;
          return obj;
        },
        {} as Record<string, string>,
      );

      return {
        build: {
          rollupOptions: {
            input: input,
          },
        },
      };
    },
    resolveId(id: string): null | string {
      return id.endsWith("main.ts") ? id : null;
    },
    load(id: string): null | string {
      if (!id.endsWith("main.ts")) return null;

      id = normalizePath(id);
      const app = id.replace(/.*\/src\//, "@/").replace("main.ts", "App.vue");
      let pluginImports = "";
      let pluginConfigs = "";
      const name = id.replace(/.*\/src\/pages\//, "").replace("/main.ts", "");
      if (config[name]) {
        const plugins = config[name];
        pluginImports = plugins
          .map((plugin) => `import { ${plugin.name} } from '${plugin.library}'`)
          .join("\n");
        pluginConfigs = plugins
          .map(
            (plugin) =>
              `app.use(${plugin.name}, ${plugin.config ? JSON.stringify(plugin.config) : undefined})`,
          )
          .join("\n");
      }
      return main
        .replace("{app}", app)
        .replace("{imports}", pluginImports)
        .replace("{configs}", pluginConfigs);
    },
  };
}
