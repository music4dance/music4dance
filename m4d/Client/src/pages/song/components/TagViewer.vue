<template>
  <div>
    <b-icon :icon="icon" :variant="variant"></b-icon>
    <tag-button :tagHandler="tagHandler" class="ml-2"></tag-button>
    <span v-if="danceId">
      on <dance-button :tagHandler="danceHandler"></dance-button
    ></span>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import DanceButton from "@/components/DanceButton.vue";
import TagButton from "@/components/TagButton.vue";
import { Tag } from "@/model/Tag";
import AdminTools from "@/mix-ins/AdminTools";
import { TagHandler } from "@/model/TagHandler";
import { DanceRating } from "@/model/DanceRating";
import { DanceHandler } from "@/model/DanceHandler";

@Component({ components: { DanceButton, TagButton } })
export default class SongPropertyViewer extends Mixins(AdminTools) {
  @Prop() private readonly tag!: Tag;
  @Prop() private readonly added!: boolean;
  @Prop() private readonly danceId?: string;

  private get tagHandler(): TagHandler {
    return new TagHandler(this.tag);
  }

  private get danceHandler(): DanceHandler {
    return new DanceHandler(
      new DanceRating({ danceId: this.danceId }),
      Tag.fromDanceId(this.danceId!)
    );
  }

  private get icon(): string {
    return this.added ? "patch-plus" : "patch-minus";
  }

  private get variant(): string {
    return this.added ? "success" : "danger";
  }
}
</script>
