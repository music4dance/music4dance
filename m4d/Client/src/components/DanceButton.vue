<template>
  <b-button
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="showModal()"
  >
    <b-icon :icon="icon"></b-icon>
    {{ tag.value }}
    <b-badge variant="light">{{ weight }}</b-badge>
    <b-icon-tags-fill
      v-if="hasTags"
      style="margin-left: 0.25em"
    ></b-icon-tags-fill>
    <dance-modal :tagHandler="danceHandler"></dance-modal>
  </b-button>
</template>

<script lang="ts">
import { Component } from "vue-property-decorator";
import DanceModal from "./DanceModal.vue";
import TagButtonBase from "./TagButtonBase";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/Song";

@Component({
  components: {
    DanceModal,
  },
})
export default class DanceButton extends TagButtonBase {
  private get danceHandler(): DanceHandler {
    return this.tagHandler as DanceHandler;
  }

  private get danceRating(): DanceRating {
    return this.danceHandler.danceRating;
  }

  private get weight(): number {
    return this.danceRating ? this.danceRating.weight : 0;
  }

  private get hasTags(): boolean {
    return this.danceRating?.tags?.length > 0;
  }
}
</script>
