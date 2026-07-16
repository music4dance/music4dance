<script setup lang="ts">
import { computed } from "vue";
import {
  type CheckboxValue,
  type CheckboxOption,
  type ButtonVariant,
  type Size,
} from "bootstrap-vue-next";
import { valuesFromOptions, isComparable } from "@/models/CheckboxTypes";
import { wordsToKebab } from "@/helpers/StringHelpers";

const model = defineModel<CheckboxValue[]>({ required: true });
const props = withDefaults(
  defineProps<{
    type: string;
    options: CheckboxOption[];
    // Result count per option, same order as `options` - when given, each option is annotated
    // with "(N)"; a nonzero option is bolded and a zero-count one is grayed out (but stays
    // checkable) once it can't produce any results given the rest of the current selection.
    // Emphasizing the available options reads more clearly than only muting the unavailable ones.
    // Omit entirely to render options exactly as before.
    counts?: number[];
    variant?: ButtonVariant;
    size?: Size;
  }>(),
  { variant: "primary", size: undefined, counts: undefined },
);

// The `#option` scoped slot only gives us back the option's `value`, not its index, so recover it
// by identity - `value` here is always the exact same object App.vue put in `options[i].value`,
// never a clone, so `===` is safe even for the non-primitive Meter values the Meter dropdown uses.
function countFor(value: CheckboxValue): number {
  const index = props.options.findIndex((o) => o.value === value);
  return index === -1 || !props.counts ? 0 : (props.counts[index] ?? 0);
}

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

function toggleAll(checked: CheckboxValue | readonly CheckboxValue[] | undefined): void {
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
    :id="computedId + '-dropdown'"
    ref="dropdown"
    :text="title"
    :variant="props.variant"
    :size="props.size"
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
        >
          <template v-if="counts" #option="{ text, value }">
            <span :class="countFor(value) === 0 ? 'text-muted' : 'fw-semibold'">
              {{ text }}
              <span class="text-muted small fw-normal">({{ countFor(value) }})</span>
            </span>
          </template>
        </BFormCheckboxGroup>
      </BFormGroup>
    </BDropdownForm>
  </BDropdown>
</template>
