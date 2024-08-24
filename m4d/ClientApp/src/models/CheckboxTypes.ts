import { wordsToKebab } from "@/helpers/StringHelpers";
import type { CheckboxOption, CheckboxValue } from "bootstrap-vue-next";

// INTO-TODO: See if we can merge ListOption into these

export interface Comparable {
  equals(that?: unknown): boolean;
}

export function isComparable(value: Comparable | unknown): value is Comparable {
  return (value as Comparable).equals !== undefined;
}

export function textFromOptions(options: CheckboxOption[]): string[] {
  return options.map(({ text }) => text);
}

export function valuesFromOptions(options: CheckboxOption[]): CheckboxValue[] {
  return options.map(({ value }) => value);
}

export function textFromValues(values: CheckboxValue[], all: CheckboxOption[]): string[] {
  return all.filter((o) => values.includes(o.value)).map((o) => o.text);
}

export function optionsFromText(text: string[]): CheckboxOption[] {
  return text.map((t) => ({ text: t, value: wordsToKebab(t) }));
}
