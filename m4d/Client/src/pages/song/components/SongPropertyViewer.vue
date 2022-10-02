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
    <component
      v-else
      v-for="(tag, index) in tags"
      :key="index"
      :is="viewer(tag)"
      :tag="tag"
      :added="isAdd"
      :danceId="danceId"
    >
    </component>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import { Tag, TagCategory } from "@/model/Tag";
import { TagList } from "@/model/TagList";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import CommentViewer from "./CommentViewer.vue";
import DanceViewer from "./DanceViewer.vue";
import TagViewer from "./TagViewer.vue";

@Component({ components: { CommentViewer, DanceViewer, TagViewer } })
export default class SongPropertyViewer extends Mixins(AdminTools) {
  @Prop() private readonly property!: SongProperty;

  private get tags(): Tag[] {
    return new TagList(this.property.value).tags;
  }

  private get isAdd(): boolean {
    return this.property.baseName.endsWith("+");
  }

  private get danceId(): string | undefined {
    return this.property.danceQualifier;
  }

  private isDance(tag: Tag): boolean {
    return tag.category === TagCategory.Dance;
  }

  private get isComment(): boolean {
    return (
      this.property.baseName === PropertyType.addCommentField ||
      this.property.baseName === PropertyType.removeCommentField
    );
  }

  private get isTempo(): boolean {
    return this.property.baseName === PropertyType.tempoField;
  }

  private viewer(tag: Tag): string {
    return this.isDance(tag) ? "dance-viewer" : "tag-viewer";
  }
}
</script>
