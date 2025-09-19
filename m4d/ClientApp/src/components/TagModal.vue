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
        class="d-flex justify-content-between align-items-start"
      >
        <div class="ms-2 me-auto">
          <div class="fw-bold">{{ option.label }}</div>
          <small class="text-muted">{{ option.description }}</small>
        </div>
        <BBadge :variant="option.variant" pill class="d-flex align-items-center">
          <!-- Icon based on option type and modifier -->
          <IBiFilter v-if="option.type === 'filter'" class="me-1" />
          <IBiList v-else-if="option.type === 'list'" class="me-1" />
          <!-- Plus or minus icon based on modifier -->
          <IBiPlus v-if="option.modifier === '+'" />
          <IBiDash v-else />
        </BBadge>
      </BListGroupItem>
    </BListGroup>

    <!-- Help Section -->
    <BListGroup>
      <BListGroupItem href="https://music4dance.blog/tag-filtering" variant="info" target="_blank">
        <div class="d-flex justify-content-between align-items-center">
          <span>Help</span>
          <IBiQuestionCircle />
        </div>
      </BListGroupItem>
    </BListGroup>
  </BModal>
</template>
