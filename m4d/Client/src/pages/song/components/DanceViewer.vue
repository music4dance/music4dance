<template>
  <div>
    <b-iconstack>
      <b-icon
        stacked
        icon="slash"
        variant="warning"
        scale="1.5"
        v-if="!added"
      ></b-icon>
      <b-icon stacked :icon="icon" :variant="variant" scale=".75"></b-icon>
    </b-iconstack>
    <dance-button :tagHandler="danceHandler" class="ml-2"></dance-button>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import DanceButton from "@/components/DanceButton.vue";
import { Tag } from "@/model/Tag";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/DanceRating";

@Component({ components: { DanceButton } })
export default class SongPropertyViewer extends Mixins(AdminTools) {
  @Prop() private readonly tag!: Tag;
  @Prop() private readonly added!: boolean;

  private get danceHandler(): DanceHandler {
    const tag = this.tag;
    return new DanceHandler(DanceRating.fromTag(tag), tag.neutral);
  }

  private get icon(): string {
    return this.positive ? "hand-thumbs-up" : "hand-thumbs-down";
  }

  private get variant(): string {
    return this.positive ? "success" : "danger";
  }

  private get positive(): boolean {
    return this.tag.positive;
  }
}
</script>
