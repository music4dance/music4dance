<template>
  <a :href="info.link" target="blank" :style="styles">
    <img :src="image" :width="width" :height="height" :alt="info.altText" />
  </a>
</template>

<script lang="ts">
import { PurchaseInfo } from "@/model/Purchase";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: {},
  props: {
    info: { type: Object as PropType<PurchaseInfo>, required: true },
    isCharm: Boolean,
    styles: String,
  },
  computed: {
    width(): number {
      return this.isCharm ? 110 : 32;
    },

    height(): number {
      return this.isCharm ? 40 : 32;
    },

    image(): string {
      return this.isCharm ? this.info.charm : this.info.logo;
    },
  },
});
</script>
