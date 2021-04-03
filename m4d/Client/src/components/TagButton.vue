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
    <tag-modal :tagHandler="tagHandler"></tag-modal>
    <b-icon v-if="selectedIcon" :icon="selectedIcon"></b-icon>
  </b-button>
</template>

<script lang="ts">
import { Component } from "vue-property-decorator";
import TagButtonBase from "./TagButtonBase";

@Component
export default class TagButton extends TagButtonBase {}
</script>
