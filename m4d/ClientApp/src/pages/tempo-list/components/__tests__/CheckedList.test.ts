import { beforeAll, describe, expect, test } from "vitest";
import { VueWrapper, mount } from "@vue/test-utils";
import CheckedList from "../CheckedList.vue";
import { optionsFromText, textFromOptions, valuesFromOptions } from "@/models/CheckboxTypes";
import { type CheckboxValue } from "bootstrap-vue-next";
import { mockResizObserver } from "@/helpers/TestHelpers";

const textA = ["First Option", "Second Option", "Third Option"];
const optionsA = optionsFromText(textA);
const valuesA = valuesFromOptions(optionsA);

function getTextForParent(wrapper: VueWrapper, selector: string): string {
  const all = wrapper.find(selector);
  expect(all).toBeDefined();
  const allParent = all.element.parentElement;
  expect(allParent).toBeDefined();
  return allParent!.textContent ?? "";
}

function checkboxGroup(wrapper: VueWrapper) {
  return wrapper.find("#test-group").findAll(".form-check");
}

describe("CheckedList.vue", () => {
  beforeAll(() => {
    mockResizObserver();
  });

  test("verify the options setup", () => {
    expect(optionsA).toBeDefined();
    expect(optionsA.length).toBe(3);
    expect(textFromOptions(optionsA)).toEqual(textA);
    expect(valuesA).toEqual(["first-option", "second-option", "third-option"]);
  });

  test("renders a simple checked list with all values checked", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: valuesA },
    });
    expect(getTextForParent(wrapper, "input[data-test='select-all']")).toBe("Un-select All");
  });

  test("renders a simple checked list with no values checked", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: [] },
    });
    expect(getTextForParent(wrapper, "input[data-test='select-all']")).toBe("Select All");
    const items = wrapper.findAll("#test-group input");
    expect(items).toBeDefined();
    expect(items.length).toBe(3);
    items.forEach((item) => {
      expect(item.attributes("checked")).toBeFalsy();
    });
  });

  test("handle checking a single item", async () => {
    // trigger("click") doesn't reliably flip a BFormCheckboxGroup checkbox in jsdom (see
    // App.test.ts's real-interaction tests, which use setValue for the same reason); switching
    // to setValue, plus listening for the camelCase "update:modelValue" event (the previous
    // hyphenated "onUpdate:model-value" key never matched Vue's emitted event name), unblocks it.
    const wrapper = mount(CheckedList, {
      attachTo: document.body,
      props: {
        type: "test",
        options: optionsA,
        modelValue: [],
        "onUpdate:modelValue": (e: CheckboxValue[]) => wrapper.setProps({ modelValue: e }),
      },
    });

    // Vue patches "checked" on a checkbox <input> as a DOM property, not an attribute, so
    // `.attributes("checked")` never reflects a true state - only `.element.checked` does.
    const items = wrapper.findAll("#test-group input");
    const checkbox = items[0]!.element as HTMLInputElement;
    expect(checkbox.checked).toBe(false);
    await items[0]?.setValue(true);
    expect(checkbox.checked).toBe(true);

    expect(getTextForParent(wrapper, "input[data-test='select-all']")).toBe("Select All");
  });

  test("renders a simple checked list with all values checked (snapshot)", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: valuesA },
    });
    expect(wrapper.html()).toMatchSnapshot();
    const items = wrapper.findAll("#test-group input");
    expect(items).toBeDefined();
    expect(items.length).toBe(3);
    items.forEach((item) => {
      expect((item.element as HTMLInputElement).checked).toBe(true);
    });
  });

  test("renders a simple checked list with no values checked (snapshot)", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: [] },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });

  test("without a counts prop, option text is unannotated", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: valuesA },
    });
    const items = checkboxGroup(wrapper);
    expect(items.map((i) => i.text())).toEqual(textA);
    expect(wrapper.find("#test-group").classes("text-muted")).toBe(false);
  });

  test("with a counts prop, each option is annotated with its count", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: valuesA, counts: [5, 0, 1] },
    });
    const items = checkboxGroup(wrapper);
    expect(items.map((i) => i.text())).toEqual([
      "First Option (5)",
      "Second Option (0)",
      "Third Option (1)",
    ]);
  });

  test("a zero-count option is grayed out but stays checkable", async () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: [], counts: [5, 0, 1] },
    });
    const items = checkboxGroup(wrapper);

    // "Second Option" has a count of 0: its label wrapper should be muted...
    expect(items[1]!.find(".form-check-label > span").classes("text-muted")).toBe(true);
    // ...while a nonzero-count option's label wrapper isn't.
    expect(items[0]!.find(".form-check-label > span").classes("text-muted")).toBe(false);

    // Graying out is purely visual - the zero-count checkbox is still fully checkable.
    const checkbox = items[1]!.find("input").element as HTMLInputElement;
    expect(checkbox.disabled).toBe(false);
    await items[1]!.find("input").setValue(true);
    expect(checkbox.checked).toBe(true);
  });
});
