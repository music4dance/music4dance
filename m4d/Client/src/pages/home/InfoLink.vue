<template>
  <a :href="link" :style="styles"
    ><slot>{{ text }}</slot></a
  >
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  props: {
    text: String,
    link: String,
    styles: [String, Array, Object],
  },
});
</script>
