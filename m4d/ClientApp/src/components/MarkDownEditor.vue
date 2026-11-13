<script setup lang="ts">
import { DanceText } from "@/models/DanceText";
import { computed, ref } from "vue";

const props = defineProps<{
  editing: boolean;
  danceName?: string;
}>();
const model = defineModel<string>({ required: true });

const initialDescription = ref(model.value);

const commit = (): void => {
  initialDescription.value = model.value;
};
const isModified = computed(() => model.value !== initialDescription.value);

defineExpose({
  commit,
  isModified,
});

const descriptionExpanded = computed(() => new DanceText(model.value).expanded(props.danceName));
</script>

<template>
  <div>
    <BFormTextarea v-if="editing" v-model="model" rows="5" max-rows="10" debounce="100" />
    <MarkdownView id="description" :markdown="descriptionExpanded" />
  </div>
</template>
