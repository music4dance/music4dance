<template>
  <div>
    <slot v-if="loaded"></slot>
    <div v-else class="d-flex justify-content-center mb-3">
      <b-spinner :label="label"></b-spinner>
    </div>
  </div>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  components: {},
  props: { loaded: Boolean, placeholder: String },
  computed: {
    label(): string {
      return this.placeholder ?? "Loading...";
    },
  },
});
</script>
