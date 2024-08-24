<script setup lang="ts">
import { computed } from "vue";
import { type CheckboxValue, type CheckboxOption } from "bootstrap-vue-next";
import { valuesFromOptions, isComparable } from "@/models/CheckboxTypes";
import { wordsToKebab } from "@/helpers/StringHelpers";

const model = defineModel<CheckboxValue[]>({ required: true });
const props = defineProps<{
  type: string;
  options: CheckboxOption[];
}>();

const title = computed(() => {
  if (allSelected.value) {
    return allDescription.value;
  } else if (indeterminate.value) {
    if (model.value.length === 1) {
      const v = model.value[0];
      if (isComparable(v)) {
        return props.options.find((e) => v.equals(e.value))!.text;
      } else {
        return props.options.find((e) => e.value === model.value[0])!.text;
      }
    } else {
      return model.value.length + " " + pluralType.value;
    }
  } else {
    return noDescription.value;
  }
});

const computedId = wordsToKebab(props.type);

const allSelected = computed(() => {
  return model.value.length === props.options.length;
});

const indeterminate = computed(() => {
  return model.value.length > 0 && !allSelected.value;
});

const allDescription = computed(() => {
  return "All " + pluralType.value;
});

const noDescription = computed(() => {
  return "No " + pluralType.value;
});

const pluralType = computed(() => {
  return props.type + "s";
});

const allValues = computed(() => {
  return valuesFromOptions(props.options);
});

function toggleAll(checked: CheckboxValue | CheckboxValue[]): any {
  model.value = checked ? allValues.value : [];
}

// Do we need this in order to emit???
// watch(model, (newVal: CheckboxValue[]) => {
//   if (newVal.length === 0) {
//     indeterminate.value = false;
//     allSelected.value = false;
//   } else if (newVal.length === props.options.length) {
//     indeterminate.value = false;
//     allSelected.value = true;
//   } else {
//     indeterminate.value = true;
//     allSelected.value = false;
//   }
//   //emit("change", newVal);
// });
</script>

<template>
  <BDropdown
    id="dropdown-form"
    ref="dropdown"
    :text="title"
    variant="primary"
    style="margin-bottom: 8px"
    auto-close="outside"
  >
    <BDropdownForm>
      <BFormCheckbox
        :id="computedId + '-all'"
        :model-value="allSelected"
        :indeterminate="indeterminate"
        aria-describedby="selected"
        aria-controls="selected"
        data-test="select-all"
        @update:model-value="toggleAll"
      >
        {{ allSelected ? "Un-select All" : "Select All" }}
      </BFormCheckbox>
      <hr />
      <BFormGroup>
        <BFormCheckboxGroup
          :id="computedId + '-group'"
          v-model="model"
          :options="options"
          name="temp"
          stacked
          data-test="checkbox-group"
        />
      </BFormGroup>
    </BDropdownForm>
  </BDropdown>
</template>
