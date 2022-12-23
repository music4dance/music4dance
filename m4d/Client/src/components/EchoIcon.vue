<template>
  <a :href="link" target="_blank" v-if="value">
    <img
      :src="image"
      v-b-tooltip.hover.click.blur.left="tip"
      width="25"
      height="25"
    />
  </a>
</template>

<script lang="ts">
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  props: {
    type: { type: String, required: true },
    value: Number,
    label: { type: String, required: true },
    maxLabel: { type: String, required: true },
  },
  computed: {
    link(): string {
      return `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${this.type}`;
    },

    image(): string {
      return `/images/icons/${this.type}-${this.level}.png`;
    },

    level(): number {
      return this.value ? Math.min(Math.floor(this.value * 10) + 1, 10) : 10;
    },

    tip(): string {
      return `This song has a ${this.label} of ${this.value?.toFixed(
        2
      )} where 1.00 is the ${this.maxLabel}.`;
    },
  },
});
</script>
