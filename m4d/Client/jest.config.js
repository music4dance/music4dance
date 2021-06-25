module.exports = {
    preset: "@vue/cli-plugin-unit-jest/presets/typescript-and-babel",
    verbose: false,
    silent: false,
    setupFilesAfterEnv: ["<rootDir>/src/jest.setup.ts"],
};
