<script setup lang="ts">
import type { AreaInfo } from "../AreaInfo";

const props = defineProps<{
  area: AreaInfo;
}>();

const link: string = props.area.link ?? `#${props.area.name}`;
const image: string = `/images/icons/${props.area.name}.png`;
const classes: string[] = [props.area.name.toLowerCase()];
</script>

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

<style scoped>
.center-block {
  display: block;
  margin-right: auto;
  margin-left: auto;
}
</style>
