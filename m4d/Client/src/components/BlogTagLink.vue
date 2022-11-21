<template>
  <div>
    <dd>
      <img src="/images/icons/info.png" alt="tools" width="24" height="24" />
      Read the <a href="https://music4dance.blog">music4dance blog</a> entries
      about <a :href="tagLink">{{ safeTitle }}</a
      >.
    </dd>
  </div>
</template>

<script lang="ts">
import { toTitleCase } from "@/helpers/StringHelpers";
import "reflect-metadata";
import { Vue } from "vue-property-decorator";

export default Vue.extend({
  props: {
    title: String,
    tag: {
      type: String,
      required: true,
    },
  },

  computed: {
    safeTitle(): string {
      return this.title ? this.title : toTitleCase(this.tag);
    },
    tagLink(): string {
      return `https://music4dance.blog/tag/${this.tag}`;
    },
  },
});
</script>
