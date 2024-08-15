<script setup lang="ts">
import { SongProperty } from "@/models/SongProperty";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { computed } from "vue";

const context = getMenuContext();

const props = defineProps<{
  name: string;
  value: string;
  editing?: boolean;
  type?: string;
  role?: string;
  isCreator?: boolean;
}>();

const emit = defineEmits<{
  "update-field": [property: SongProperty];
}>();

const internalValue = computed({
  get: () => props.value,
  set: (value: string) => {
    emit("update-field", new SongProperty({ name: props.name, value }));
  },
});

const isNumber = computed(() => props.type === "number");
const computedType = computed(() => props.type ?? "text");
const hasEditPermission = computed(() => {
  const role = props.role;
  return !!props.isCreator || (!!role && context.hasRole(role));
});
</script>

<template>
  <span>
    <input
      v-if="editing && hasEditPermission"
      v-model="internalValue"
      :type="computedType"
      class="form-control ml-2"
      style="display: inline"
      :class="{ number: isNumber, text: !isNumber }"
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
