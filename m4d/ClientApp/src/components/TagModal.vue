<script setup lang="ts">
import { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";
import TagIcon from "./TagIcon.vue";

const props = defineProps<{
  visible: boolean;
  // INT-TODO: For some reason typescript is generating an error on thi in TagCloud if
  //  I enforce the type
  tagHandler: TagHandler | any;
}>();

defineEmits<{
  update: [value: boolean];
}>();

const handler = computed(() => props.tagHandler);

const tag = computed(() => handler.value.tag);

const title = computed(() => {
  const parent = handler.value.parent;
  return parent ? parent.description : tag.value.value;
});

const includeOnly = computed(() => {
  return getTagLink("+", true);
});

const excludeOnly = computed(() => {
  return getTagLink("-", true);
});

const includeTag = computed(() => {
  return getTagLink("+", false);
});

const excludeTag = computed(() => {
  return getTagLink("-", false);
});

const hasFilter = computed(() => {
  const filter = handler.value.filter;
  return !!filter && !filter.isDefault(handler.value.user) && !filter.isRaw;
});

const singleDance = computed(() => {
  return handler.value.filter?.singleDance ?? false;
});

const danceName = computed(() => {
  return handler.value.filter?.danceQuery.danceNames[0] ?? "ERROR";
});

function getTagLink(modifier: string, exclusive: boolean): string {
  let link = `/song/addtags/?tags=${encodeURIComponent(modifier + tag.value.key)}`;
  const filter = handler.value.filter;
  if (hasFilter.value && !exclusive) {
    link = link + `&filter=${filter!.encodedQuery}`;
  } else if (filter && filter.isDefault(handler.value.user)) {
    link = link + `&filter=${filter.extractDefault(handler.value.user).encodedQuery}`;
  }
  return link;
}
</script>

<template>
  <BModal
    :id="handler.id"
    :model-value="visible"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    hide-footer
    @update:model-value="$emit('update', $event)"
  >
    <template #title><TagIcon :name="tag.icon!" />&nbsp;{{ title }} </template>
    <!-- <template v-slot:title>Help Me!</template> -->
    <BListGroup>
      <BListGroupItem v-if="hasFilter" :href="includeTag" variant="success">
        <span v-if="singleDance"> List all {{ danceName }} </span>
        <span v-else> Filter the list to include only </span>
        songs tagged as <em>{{ tag.value }}</em>
      </BListGroupItem>
      <BListGroupItem v-if="hasFilter" :href="excludeTag" variant="danger">
        <span v-if="singleDance"> List all {{ danceName }} </span>
        <span v-else> Filter the list to include only </span>
        songs <b>not</b> tagged as
        <em>{{ tag.value }}</em>
      </BListGroupItem>
      <BListGroupItem :href="includeOnly" variant="success">
        List all songs tagged as <em>{{ tag.value }}</em>
      </BListGroupItem>
      <BListGroupItem :href="excludeOnly" variant="danger">
        List all songs <b>not</b> tagged as <em>{{ tag.value }}</em>
      </BListGroupItem>
      <BListGroupItem href="https://music4dance.blog/tag-filtering" variant="info" target="_blank"
        >Help</BListGroupItem
      >
    </BListGroup>
  </BModal>
</template>
