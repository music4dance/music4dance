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
import DanceButton from "@/components/DanceButton.vue";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/DanceRating";
import { Tag } from "@/model/Tag";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceButton },
  props: {
    tag: { type: Object as PropType<Tag>, required: true },
    added: Boolean,
  },
  computed: {
    danceHandler(): DanceHandler {
      const tag = this.tag;
      return new DanceHandler(DanceRating.fromTag(tag), tag.neutral);
    },

    icon(): string {
      return this.positive ? "hand-thumbs-up" : "hand-thumbs-down";
    },

    variant(): string {
      return this.positive ? "success" : "danger";
    },

    positive(): boolean {
      return this.tag.positive;
    },
  },
});
</script>
