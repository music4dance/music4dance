<template>
  <div style="display: flex">
    <b-icon :icon="icon" :variant="variant"></b-icon>
    <span>
      <tag-button :tagHandler="tagHandler" class="ml-2"></tag-button>
      <span v-if="danceId">
        on <dance-button :tagHandler="danceHandler"></dance-button
      ></span>
    </span>
  </div>
</template>

<script lang="ts">
import DanceButton from "@/components/DanceButton.vue";
import TagButton from "@/components/TagButton.vue";
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/DanceRating";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { DanceButton, TagButton },
  props: {
    tag: { type: Object as PropType<Tag>, required: true },
    added: Boolean,
    danceId: String,
  },
  computed: {
    tagHandler(): TagHandler {
      return new TagHandler(this.tag);
    },

    danceHandler(): DanceHandler {
      return new DanceHandler(
        new DanceRating({ danceId: this.danceId }),
        Tag.fromDanceId(this.danceId!)
      );
    },

    icon(): string {
      return this.added ? "patch-plus" : "patch-minus";
    },

    variant(): string {
      return this.added ? "success" : "danger";
    },
  },
});
</script>
