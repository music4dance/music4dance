/* eslint-env node */
require("@rushstack/eslint-patch/modern-module-resolution");

module.exports = {
  root: true,
  extends: [
    "plugin:vue/vue3-recommended",
    "eslint:recommended",
    "@vue/eslint-config-typescript",
    "@vue/eslint-config-prettier/skip-formatting",
  ],
  rules: {
    "vue/no-v-html": "off",
    "vue/component-name-in-template-casing": ["error", "PascalCase"],
  },
  parserOptions: {
    ecmaVersion: "latest",
  },
};
