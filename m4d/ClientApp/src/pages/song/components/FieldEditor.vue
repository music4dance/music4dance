<script setup lang="ts">
import { SongProperty } from "@/models/SongProperty";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed, ref, watch } from "vue";

const context = getMenuContext();

const props = defineProps<{
  name: string;
  value: string;
  editing?: boolean;
  type?: string;
  role?: string;
  isCreator?: boolean;
  overridePermission?: boolean;
}>();

const emit = defineEmits<{
  "update-field": [property: SongProperty];
}>();

// Local value lets the user see real-time changes (including arrow-key increments)
// without immediately calling modifyProperty. The value is only committed—and
// update-field emitted—when the user explicitly blurs the field or presses Enter.
const localValue = ref(props.value);
watch(
  () => props.value,
  (newVal) => {
    localValue.value = newVal;
  },
);
const commitValue = () => {
  if (localValue.value !== props.value) {
    emit("update-field", new SongProperty({ name: props.name, value: localValue.value }));
  }
};

const isNumber = computed(() => props.type === "number");
const computedType = computed(() => props.type ?? "text");
const hasEditPermission = computed(() => {
  const role = props.role;

  if (props.overridePermission) {
    return true;
  }

  return !!props.isCreator || (!!role && context.hasRole(role));
});
</script>

<template>
  <span>
    <input
      v-if="editing && hasEditPermission"
      v-model="localValue"
      :type="computedType"
      class="form-control ml-2"
      style="display: inline"
      :class="{ number: isNumber, text: !isNumber }"
      @blur="commitValue"
      @keyup.enter="commitValue"
    />
    <span v-else
      ><slot>{{ value }}</slot></span
    >
  </span>
</template>

<style lang="scss" scoped>
.number {
  width: 5em;
}
.text {
  width: auto;
}
</style>
