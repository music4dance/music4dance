<template>
  <b-button
    block
    variant="outline-primary"
    :aria-expanded="showExtra ? 'true' : 'false'"
    :aria-controls="extraId"
    @click="$emit('change', !showExtra)"
  >
    <b-icon-triangle :flip-v="!showExtra" class="mx-3"></b-icon-triangle
    >{{ buttonText }}
    <b-icon-triangle :flip-v="!showExtra" class="mx-3"></b-icon-triangle
  ></b-button>
</template>

<script lang="ts">
import "reflect-metadata";
import Vue from "vue";

export default Vue.extend({
  model: {
    prop: "showExtra",
    event: "change",
  },
  props: {
    showExtra: Boolean,
    extraId: Boolean,
  },
  computed: {
    buttonText(): string {
      return this.showExtra ? "Show Less" : "Show More";
    },
  },
});
</script>
