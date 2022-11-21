<template>
  <div class="col-sm">
    <a :href="link">
      <img
        :src="image"
        :alt="area.name"
        role="button"
        width="128"
        height="128"
        class="center-block"
      />
      <h2 class="center-block" :class="classes" style="text-align: center">
        {{ area.name }}
      </h2>
    </a>
  </div>
</template>

<script lang="ts">
import Vue, { PropType } from "vue";
import type { AreaInfo } from "./AreaInfo";

export default Vue.extend({
  props: {
    area: {
      type: Object as PropType<AreaInfo>,
      required: true,
    },
  },
  computed: {
    link(): string {
      return this.area.link ?? `#${this.area.name}`;
    },
    image(): string {
      return `/images/icons/${this.area.name}.png`;
    },
    classes(): string[] {
      return [this.area.name.toLowerCase()];
    },
  },
});
</script>
