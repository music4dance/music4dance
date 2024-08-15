<script setup lang="ts">
import { camelToPascal } from "@/helpers/StringHelpers";
import { computed } from "vue";

const props = defineProps<{
  name: string;
  value: string;
  canAdd?: boolean;
}>();

const emit = defineEmits<{
  "add-property": [name: string, value: string];
}>();

const friendlyName = computed(() => camelToPascal(props.name));
const onAdd = () => {
  emit("add-property", props.name, props.value);
};
</script>

<template>
  <div>
    <BButton
      v-if="canAdd"
      variant="outline-primary"
      size="sm"
      block
      style="text-align: left"
      class="mb-2"
      @click="onAdd"
    >
      <IBiLockFill aria-label="add" />
      {{ friendlyName }}: <b>{{ value }}</b>
    </BButton>
    <div v-else>
      {{ friendlyName }}: <b>{{ value }}</b>
    </div>
  </div>
</template>
