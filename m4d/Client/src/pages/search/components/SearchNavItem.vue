<template>
  <b-nav pills
    ><b-nav-item :active="isActive" @click="onClick">{{
      title
    }}</b-nav-item></b-nav
  >
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  props: {
    id: { type: String, required: true },
    title: { type: String, required: true },
    active: { type: String, required: true },
  },
  computed: {
    isActive(): boolean {
      return this.active === this.id;
    },
  },
  methods: {
    onClick(): void {
      window.location.hash = `#${this.id}`;
    },
  },
});
</script>
