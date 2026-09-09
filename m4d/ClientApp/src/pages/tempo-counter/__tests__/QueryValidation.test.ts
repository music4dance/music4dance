import { describe, test, expect } from "vitest";
import {
  DEFAULT_COUNT_METHOD,
  DEFAULT_EPSILON,
  DEFAULT_NUMERATOR,
  DEFAULT_TEMPO,
  MAX_EPSILON,
  MAX_TEMPO,
  MIN_EPSILON,
  MIN_TEMPO,
  validateCountMethod,
  validateEpsilon,
  validateNumerator,
  validateTempo,
} from "../QueryValidation";

describe("validateNumerator", () => {
  test.each([2, 3, 4])("accepts %i (a real dance meter this page offers)", (value) => {
    expect(validateNumerator(value)).toBe(value);
  });

  test("falls back to the default for a numerator no dance uses", () => {
    expect(validateNumerator(7)).toBe(DEFAULT_NUMERATOR);
  });

  test("falls back to the default when undefined", () => {
    expect(validateNumerator(undefined)).toBe(DEFAULT_NUMERATOR);
  });
});

describe("validateTempo", () => {
  test("accepts a value within [0, 500]", () => {
    expect(validateTempo(124)).toBe(124);
    expect(validateTempo(MIN_TEMPO)).toBe(MIN_TEMPO);
    expect(validateTempo(MAX_TEMPO)).toBe(MAX_TEMPO);
  });

  test("falls back to the default below the minimum", () => {
    expect(validateTempo(-1)).toBe(DEFAULT_TEMPO);
  });

  test("falls back to the default above the maximum", () => {
    expect(validateTempo(MAX_TEMPO + 1)).toBe(DEFAULT_TEMPO);
  });

  test("falls back to the default for non-finite values", () => {
    expect(validateTempo(Number.NaN)).toBe(DEFAULT_TEMPO);
    expect(validateTempo(Number.POSITIVE_INFINITY)).toBe(DEFAULT_TEMPO);
  });

  test("falls back to the default when undefined", () => {
    expect(validateTempo(undefined)).toBe(DEFAULT_TEMPO);
  });
});

describe("validateCountMethod", () => {
  test("passes through 'measures'", () => {
    expect(validateCountMethod("measures")).toBe("measures");
  });

  test("passes through 'beats'", () => {
    expect(validateCountMethod("beats")).toBe("beats");
  });

  test("falls back to the default for an unrecognized value", () => {
    expect(validateCountMethod("not-a-count-method")).toBe(DEFAULT_COUNT_METHOD);
  });

  test("falls back to the default when undefined", () => {
    expect(validateCountMethod(undefined)).toBe(DEFAULT_COUNT_METHOD);
  });
});

describe("validateEpsilon", () => {
  test("accepts a value within [0, 20]", () => {
    expect(validateEpsilon(12)).toBe(12);
    expect(validateEpsilon(MIN_EPSILON)).toBe(MIN_EPSILON);
    expect(validateEpsilon(MAX_EPSILON)).toBe(MAX_EPSILON);
  });

  test("falls back to the default below the minimum", () => {
    expect(validateEpsilon(-1)).toBe(DEFAULT_EPSILON);
  });

  test("falls back to the default above the maximum", () => {
    expect(validateEpsilon(MAX_EPSILON + 1)).toBe(DEFAULT_EPSILON);
  });

  test("falls back to the default for non-finite values", () => {
    expect(validateEpsilon(Number.NaN)).toBe(DEFAULT_EPSILON);
  });

  test("falls back to the default when undefined", () => {
    expect(validateEpsilon(undefined)).toBe(DEFAULT_EPSILON);
  });
});
