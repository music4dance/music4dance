<script setup lang="ts">
import { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";

const props = defineProps<{
  tagHandler: TagHandler;
}>();

const modelValue = defineModel<boolean>({ default: false });

const handler = computed(() => props.tagHandler);

const tag = computed(() => handler.value.tag);

const title = computed(() => {
  const parent = handler.value.parent;
  return parent ? parent.description : tag.value.value;
});

const availableOptions = computed(() => handler.value.getAvailableOptions());
</script>

<template>
  <BModal
    :id="handler.id"
    v-model="modelValue"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    no-footer
  >
    <template #title><TagIcon :name="tag.icon!" />&nbsp;{{ title }} </template>
    <!-- Single unified options list -->
    <BListGroup class="mb-2">
      <BListGroupItem
        v-for="option in availableOptions"
        :key="`${option.scope}-${option.type}-${option.modifier}`"
        :href="option.href"
        :variant="option.variant"
      >
        {{ option.label }}
      </BListGroupItem>
    </BListGroup>

    <!-- Help Section -->
    <BListGroup>
      <BListGroupItem href="https://music4dance.blog/tag-filtering" variant="info" target="_blank">
        Help
      </BListGroupItem>
    </BListGroup>
  </BModal>
</template>
