import { describe, expect, test } from "vitest";
import { assign } from "../ObjectHelpers";

describe("ObjectHelpers.ts", () => {
  describe("assign", () => {
    test("should assign properties from source object to target object", () => {
      const target = { a: 1, b: 2 };
      const source = { b: 3, c: 4 };
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({ a: 1, b: 3, c: 4 });
    });

    test("should not assign non-enumerable properties", () => {
      const target = {};
      const source = {};
      Object.defineProperty(source, "a", { value: 1, enumerable: false });
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({});
    });

    test("should not assign non-writable properties", () => {
      const target = {};
      const source = {};
      Object.defineProperty(source, "a", { value: 1, writable: false });
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({});
    });

    test("should not assign get-only properties", () => {
      const target = {};
      const source = {
        get a() {
          return 1;
        },
      };
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({});
    });

    test("should handle empty source object", () => {
      const target = { a: 1, b: 2 };
      const source = {};
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({ a: 1, b: 2 });
    });

    test("should handle empty target object", () => {
      const target = {};
      const source = { a: 1, b: 2 };
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({ a: 1, b: 2 });
    });

    test("should handle empty target and source objects", () => {
      const target = {};
      const source = {};
      const result = assign(target, source);
      expect(result).toBe(target);
      expect(result).toEqual({});
    });
  });
});
