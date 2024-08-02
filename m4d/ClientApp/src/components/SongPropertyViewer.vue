<script setup lang="ts">
import { PropertyType, SongProperty } from "@/models/SongProperty";
import { Tag, TagCategory } from "@/models/Tag";
import { TagList } from "@/models/TagList";
import CommentViewer from "./CommentViewer.vue";
import DanceViewer from "./DanceViewer.vue";
import TagViewer from "./TagViewer.vue";
import type { DanceHandler } from "@/models/DanceHandler";
import type { TagHandler } from "@/models/TagHandler";

const props = defineProps<{
  property: SongProperty;
}>();

const emit = defineEmits<{
  "dance-clicked": [handler: DanceHandler];
  "tag-clicked": [handler: TagHandler];
}>();

const tags = new TagList(props.property.value).tags;
const isAdd = props.property.baseName.endsWith("+");
const danceId = props.property.danceQualifier;
const isComment =
  props.property.baseName === PropertyType.addCommentField ||
  props.property.baseName === PropertyType.removeCommentField;
const isTempo = props.property.baseName === PropertyType.tempoField;
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
        @dance-clicked="emit('dance-clicked', $event as DanceHandler)"
        @tag-clicked="emit('tag-clicked', $event as TagHandler)"
      >
      </component>
    </div>
  </div>
</template>
import type { DanceHandler } from "@/models/DanceHandler"; import type { TagHandler } from
"@/models/TagHandler";
