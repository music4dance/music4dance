<template>
  <div style="display: flex">
    <b-icon :icon="icon" :variant="variant"></b-icon>
    <span>
      {{ comment }}
      <span v-if="danceId">
        on <dance-button :tagHandler="danceHandler"></dance-button
      ></span>
    </span>
  </div>
</template>

<script lang="ts">
import DanceButton from "@/components/DanceButton.vue";
import TagButton from "@/components/TagButton.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/DanceRating";
import { Tag } from "@/model/Tag";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({ components: { DanceButton, TagButton } })
export default class CommentViewer extends Mixins(AdminTools) {
  @Prop() private readonly comment!: string;
  @Prop() private readonly added!: boolean;
  @Prop() private readonly danceId?: string;

  private get danceHandler(): DanceHandler | undefined {
    return this.danceId
      ? new DanceHandler(
          new DanceRating({ danceId: this.danceId }),
          Tag.fromDanceId(this.danceId!)
        )
      : undefined;
  }

  private get icon(): string {
    return this.added ? "patch-plus" : "patch-minus";
  }

  private get variant(): string {
    return this.added ? "success" : "danger";
  }
}
</script>
