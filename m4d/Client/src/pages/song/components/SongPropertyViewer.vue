<template>
  <div>
    <comment-viewer
      v-if="isComment"
      :comment="property.value"
      :added="isAdd"
      :danceId="danceId"
    >
    </comment-viewer>
    <span v-else-if="isTempo"> tempo = {{ property.value }} BPM</span>
    <div v-else>
      <component
        v-for="(tag, index) in tags"
        :key="index"
        :is="viewer(tag)"
        :tag="tag"
        :added="isAdd"
        :danceId="danceId"
      >
      </component>
    </div>
  </div>
</template>

<script lang="ts">
import { PropertyType, SongProperty } from "@/model/SongProperty";
import { Tag, TagCategory } from "@/model/Tag";
import { TagList } from "@/model/TagList";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import CommentViewer from "./CommentViewer.vue";
import DanceViewer from "./DanceViewer.vue";
import TagViewer from "./TagViewer.vue";

export default Vue.extend({
  components: { CommentViewer, DanceViewer, TagViewer },
  props: {
    property: { type: Object as PropType<SongProperty>, required: true },
  },
  computed: {
    tags(): Tag[] {
      return new TagList(this.property.value).tags;
    },
    isAdd(): boolean {
      return this.property.baseName.endsWith("+");
    },
    danceId(): string | undefined {
      return this.property.danceQualifier;
    },
    isComment(): boolean {
      return (
        this.property.baseName === PropertyType.addCommentField ||
        this.property.baseName === PropertyType.removeCommentField
      );
    },
    isTempo(): boolean {
      return this.property.baseName === PropertyType.tempoField;
    },
  },
  methods: {
    viewer(tag: Tag): string {
      return this.isDance(tag) ? "dance-viewer" : "tag-viewer";
    },
    isDance(tag: Tag): boolean {
      return tag.category === TagCategory.Dance;
    },
  },
});
</script>
