import pluginVue from "eslint-plugin-vue";
import vueTsEslintConfig from "@vue/eslint-config-typescript";
import pluginVitest from "@vitest/eslint-plugin";
import skipFormatting from "@vue/eslint-config-prettier";

export default [
  {
    name: "app/files-to-lint",
    files: ["**/*.{ts,mts,tsx,vue}"],
  },
  {
    name: "app/files-to-ignore",
    // __snapshots__ holds vitest-generated files - their formatting is vitest's
    // to decide, not prettier's.
    ignores: ["**/dist/**", "**/dist-ssr/**", "**/coverage/**", "**/__snapshots__/**"],
  },
  ...pluginVue.configs["flat/recommended"],
  ...vueTsEslintConfig(),
  {
    ...pluginVitest.configs.recommended,
    files: ["src/**/*.spec.*", "src/**/*.test.*"],
  },
  skipFormatting,
  {
    rules: {
      "vue/no-v-html": "off",
      "vue/component-name-in-template-casing": [
        "error",
        "PascalCase",
        { registeredComponentsOnly: false },
      ],
      "vue/html-self-closing": [
        "error",
        {
          html: {
            void: "always",
            normal: "always",
            component: "always",
          },
          svg: "always",
          math: "always",
        },
      ],
      "vitest/expect-expect": "off",
    },
  },
  {
    // Test files declare throwaway stub components inline, which is the point of
    // a stub - the single-file-component authoring rules don't apply to them.
    name: "app/test-stub-components",
    files: ["src/**/*.spec.*", "src/**/*.test.*"],
    rules: {
      "vue/one-component-per-file": "off",
      "vue/require-prop-types": "off",
    },
  },
];
