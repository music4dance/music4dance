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
});
