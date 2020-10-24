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
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { toTitleCase } from "@/helpers/StringHelpers";

@Component
export default class BlogTagLink extends Vue {
  @Prop() private readonly title?: string;
  @Prop() private readonly tag!: string;

  private get icon(): string {
    return `/images/icons/info.png`;
  }

  private get safeTitle(): string {
    return this.title ? this.title : toTitleCase(this.tag);
  }

  private get tagLink(): string {
    return `https://music4dance.blog/tag/${this.tag}`;
  }
}
</script>
