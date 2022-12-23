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
import { DanceHandler } from "@/model/DanceHandler";
import { DanceRating } from "@/model/DanceRating";
import { Tag } from "@/model/Tag";
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  components: { DanceButton },
  props: {
    comment: { type: String, required: true },
    added: Boolean,
    danceId: String,
  },
  computed: {
    danceHandler(): DanceHandler | undefined {
      return this.danceId
        ? new DanceHandler(
            new DanceRating({ danceId: this.danceId }),
            Tag.fromDanceId(this.danceId!)
          )
        : undefined;
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
