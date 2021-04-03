<template>
  <b-badge
    :title="tag.value"
    :variant="variant"
    hef="#"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="onClick"
    role="button"
  >
    <b-icon :icon="icon"></b-icon>
    {{ tag.value }}
    <b-icon-plus-circle></b-icon-plus-circle>
  </b-badge>
</template>

<script lang="ts">
import { Component } from "vue-property-decorator";
import TagButtonBase from "./TagButtonBase";

@Component
export default class TagButtonOther extends TagButtonBase {
  private onClick(): void {
    this.$emit("add-tag", this.tagHandler.tag);
  }
}
</script>
