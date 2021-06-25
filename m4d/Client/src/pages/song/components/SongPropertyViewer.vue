<template>
  <div>
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
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import DanceViewer from "./DanceViewer.vue";
import TagViewer from "./TagViewer.vue";
import { Tag, TagCategory } from "@/model/Tag";
import { TagList } from "@/model/TagList";
import AdminTools from "@/mix-ins/AdminTools";

@Component({ components: { DanceViewer, TagViewer } })
export default class SongPropertyViewer extends Mixins(AdminTools) {
  @Prop() private readonly property!: SongProperty;

  private get tags(): Tag[] {
    return new TagList(this.property.value).tags;
  }

  private get isAdd(): boolean {
    return this.property.baseName === PropertyType.addedTags;
  }

  private get danceId(): string | undefined {
    return this.property.danceQualifier;
  }

  private isDance(tag: Tag): boolean {
    return tag.category === TagCategory.Dance;
  }

  private viewer(tag: Tag): string {
    return this.isDance(tag) ? "dance-viewer" : "tag-viewer";
  }
}
</script>
