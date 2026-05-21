<script setup lang="ts">
import { computed } from "vue";
import { PropertyType, SongProperty } from "@/models/SongProperty";
import { Tag, TagCategory } from "@/models/Tag";
import { TagList } from "@/models/TagList";
import type { DanceHandler } from "@/models/DanceHandler";
import type { TagHandler } from "@/models/TagHandler";
import TagViewer from "@/components/TagViewer.vue";
import DanceViewer from "@/components/DanceViewer.vue";

const props = defineProps<{
  property: SongProperty;
  activeTags?: Set<string>;
}>();

const emit = defineEmits<{
  "dance-clicked": [handler: DanceHandler];
  "tag-clicked": [handler: TagHandler];
}>();

const tags = computed(() => new TagList(props.property.value).tags);
const isAdd = computed(() => props.property.baseName.endsWith("+"));
const danceId = computed(() => props.property.danceQualifier);
const isComment = computed(
  () =>
    props.property.baseName === PropertyType.addCommentField ||
    props.property.baseName === PropertyType.removeCommentField,
);
const isTempo = computed(() => props.property.baseName === PropertyType.tempoField);
const viewer = (tag: Tag) => (isDance(tag) ? DanceViewer : TagViewer);
const isDance = (tag: Tag) => tag.category === TagCategory.Dance;
</script>

<template>
  <div>
    <CommentViewer v-if="isComment" :comment="property.value" :added="isAdd" :dance-id="danceId" />
    <span v-else-if="isTempo"> tempo = {{ property.value }} BPM</span>
    <div v-else>
      <component
        :is="viewer(tag)"
        v-for="(tag, index) in tags"
        :key="index"
        :tag="tag"
        :added="isAdd"
        :dance-id="danceId"
        :active-tags="activeTags"
        @dance-clicked="emit('dance-clicked', $event as DanceHandler)"
        @tag-clicked="emit('tag-clicked', $event as TagHandler)"
      />
    </div>
  </div>
</template>
