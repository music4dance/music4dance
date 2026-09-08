import { describe, test, expect, beforeEach, vi } from "vitest";
import { nextTick, ref } from "vue";
import { buildQueryString, useUrlQuerySync } from "../useUrlQuerySync";

describe("buildQueryString", () => {
  test("returns an empty string when there are no params", () => {
    expect(buildQueryString({})).toBe("");
  });

  test("omits undefined values entirely rather than serializing 'key='", () => {
    expect(buildQueryString({ a: "1", b: undefined })).toBe("?a=1");
  });

  test("omits empty-string values", () => {
    expect(buildQueryString({ a: "", b: "1" })).toBe("?b=1");
  });

  test("serializes an array as repeated keys, matching ASP.NET Core's List<string> binding", () => {
    expect(buildQueryString({ styles: ["waltz", "tango"] })).toBe("?styles=waltz&styles=tango");
  });

  test("omits an empty array entirely (no repeated keys to write)", () => {
    expect(buildQueryString({ styles: [] })).toBe("");
  });

  test("combines scalar and array params in insertion order", () => {
    expect(buildQueryString({ tempo: "120", styles: ["waltz", "tango"] })).toBe(
      "?tempo=120&styles=waltz&styles=tango",
    );
  });
});

describe("useUrlQuerySync", () => {
  beforeEach(() => {
    window.history.replaceState(null, "", "/Home/Counter");
  });

  test("writes the initial params to the address bar as soon as it's called", () => {
    useUrlQuerySync(() => ({ numerator: "4", tempo: "120" }));

    expect(window.location.search).toBe("?numerator=4&tempo=120");
  });

  test("keeps the address bar in sync as the underlying reactive state changes", async () => {
    const tempo = ref("120");
    useUrlQuerySync(() => ({ tempo: tempo.value }));

    tempo.value = "132";
    await nextTick();

    expect(window.location.search).toBe("?tempo=132");
  });

  test("preserves the current pathname and hash, only replacing the query string", () => {
    window.history.replaceState(null, "", "/Home/Counter#section");

    useUrlQuerySync(() => ({ tempo: "120" }));

    expect(window.location.pathname).toBe("/Home/Counter");
    expect(window.location.search).toBe("?tempo=120");
    expect(window.location.hash).toBe("#section");
  });

  test("uses history.replaceState rather than pushState, so no new history entry is created", () => {
    const replaceSpy = vi.spyOn(window.history, "replaceState");

    useUrlQuerySync(() => ({ tempo: "120" }));

    expect(replaceSpy).toHaveBeenCalled();
    replaceSpy.mockRestore();
  });
});
