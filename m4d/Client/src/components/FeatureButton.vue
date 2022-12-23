<template>
  <b-button
    v-if="link"
    :variant="safeVariant"
    size="sm"
    style="margin-left: 1em"
    :href="reference"
  >
    <b-icon :icon="icon"></b-icon>&nbsp;{{ title }}
  </b-button>
</template>

<script lang="ts">
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  props: {
    title: { type: String, required: true },
    type: { type: String, required: true },
    link: String,
    variant: String,
  },
  computed: {
    icon(): string {
      switch (this.type) {
        case "play":
          return "play";
        case "docs":
          return "file-text";
        case "blog":
          return "pencil-square";
        default:
          throw new Error(this.typeError);
      }
    },
    reference(): string {
      if (this.link.startsWith("https")) {
        return this.link;
      }
      switch (this.type) {
        case "play":
          return `https://www.music4dance.net${this.link}`;
        case "docs":
          return `https://music4dance.blog/music4dance-help${this.link}`;
        case "blog":
          return `https://music4dance.blog${this.link}`;
        default:
          throw new Error(this.typeError);
      }
    },
    safeVariant(): string {
      return this.variant ? this.variant.toLowerCase() : "primary";
    },
    typeError(): string {
      return `${this.type} is not a valid type for Feature Button`;
    },
  },
});
</script>
