<script setup lang="ts">
import { computed } from "vue";

interface Props {
  /**
   * Available style families for selection (e.g., ["International", "American", "Country"])
   */
  styles: string[];
  /**
   * Currently selected style (if any)
   */
  modelValue?: string;
  /**
   * Size of the buttons
   */
  size?: "sm" | "md" | "lg";
  /**
   * Whether to show as pills (true) or regular buttons (false)
   */
  pill?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: undefined,
  size: "sm",
  pill: false,
});

const emit = defineEmits<{
  "update:modelValue": [style: string];
}>();

const selectStyle = (style: string): void => {
  emit("update:modelValue", style);
};

const isSelected = (style: string): boolean => {
  return props.modelValue === style;
};

const hasSingleStyle = computed(() => props.styles.length === 1);
</script>

<template>
  <BButtonGroup v-if="styles.length > 0" :size="size">
    <BButton
      v-for="style in styles"
      :key="style"
      :variant="isSelected(style) ? 'primary' : 'outline-primary'"
      :pill="pill"
      :disabled="hasSingleStyle"
      @click="selectStyle(style)"
    >
      {{ style }}
    </BButton>
  </BButtonGroup>
</template>
