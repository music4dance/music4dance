import { fileURLToPath, URL } from "node:url";

import { defineConfig, normalizePath, Plugin, UserConfig } from "vite";
import { dirname, resolve } from "path";
import vue from "@vitejs/plugin-vue";
import mkcert from "vite-plugin-mkcert";
import Components from "unplugin-vue-components/vite";
import { BootstrapVueNextResolver } from "unplugin-vue-components/resolvers";
import Inspect from "vite-plugin-inspect";
import fg from "fast-glob";
import Icons from "unplugin-icons/vite";
import IconsResolve from "unplugin-icons/resolver";

// @ts-ignore
import appsettingsDev from "../appsettings.Development.json";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    Inspect({ build: true, outputDir: ".vite-inspect" }),
    vue(),
    mkcert(),
    AutoEndpoints(),
    Components({
      resolvers: [BootstrapVueNextResolver(), IconsResolve()],
      dts: true,
    }),
    Icons({ compiler: "vue3", autoInstall: true }),
  ],
  base: "/vclient",
  server: {
    port: appsettingsDev.Vite.Server.Port,
    https: true,
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
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
});

// This plugin will find all files matching the pattern src/pages/*/App.vue
//  and make endpoints with the 'name' being the replacement for the *. and the
//  path being src/pages/*/main.ts - then return a template for the main.ts file
function AutoEndpoints(): Plugin {
  const main = `import 'vite/modulepreload-polyfill'
import '@/scss/styles.scss'

import { createApp } from 'vue'
import App from '{app}'

createApp(App).mount('#app')
`;

  return {
    name: "auto-endpoints",
    async config(): Promise<UserConfig> {
      const root = "src/pages/";
      const pattern = root + "*/App.vue";
      const length = root.length;
      const dirs = (await fg.glob(pattern)).map((p) => dirname(p).substring(length));
      console.log(dirs.join(","));

      const input = dirs.reduce((obj, item) => {
        const value = resolve(__dirname, root + item + "/main.ts");
        obj[item] = value;
        return obj;
      }, {} as any);

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
      return main.replace("{app}", app);
    },
  };
}
