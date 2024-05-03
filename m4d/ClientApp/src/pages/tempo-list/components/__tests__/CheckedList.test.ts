import { describe, expect, test } from "vitest";
import { VueWrapper, mount } from "@vue/test-utils";
import CheckedList from "../CheckedList.vue";
import { optionsFromText, textFromOptions, valuesFromOptions } from "@/models/CheckboxTypes";
import { type CheckboxValue } from "bootstrap-vue-next";

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

  test.skip("handle checking a single item", async () => {
    // TODO: Get this test working - I suspect I'm doing something wrong with
    //  handing of models/events especially as they work with respect to checkbox controls
    const wrapper = mount(CheckedList, {
      attachTo: document.body,
      propsData: { type: "test", options: optionsA, modelValue: [] },
      "onUpdate:model-value": (e: CheckboxValue[]) => {
        //console.log("onUpdate:modelValue", JSON.stringify(e));
        wrapper.setProps({ modelValue: e });
      },
    });

    //const all = wrapper.get("input[data-test='select-all']");
    //console.log("Initial classes:", all.classes().join(", "));

    const items = wrapper.findAll("#test-group input");
    expect(items[0].attributes("checked")).toBeFalsy();
    await items[0].trigger("click");
    //const emits = wrapper.emitted("update:modelValue");
    //console.log("Emits:", JSON.stringify(emits));
    expect(items[0].attributes("checked")).toBeTruthy();

    expect(getTextForParent(wrapper, "input[data-test='select-all']")).toBe("Select All");
    //const items = wrapper.findAll("#test-group input");
  });

  test.skip("renders a simple checked list with all values checked (snapshot)", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: valuesA },
    });
    expect(wrapper.html()).toMatchSnapshot();
    const items = wrapper.findAll("#test-group input");
    expect(items).toBeDefined();
    expect(items.length).toBe(3);
    items.forEach((item) => {
      expect(item.attributes("checked")).toBeTruthy();
    });
  });

  test.skip("renders a simple checked list with no values checked (snapshot)", () => {
    const wrapper = mount(CheckedList, {
      propsData: { type: "test", options: optionsA, modelValue: [] },
    });
    expect(wrapper.html()).toMatchSnapshot();
  });
});
