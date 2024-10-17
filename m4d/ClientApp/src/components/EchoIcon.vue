<script setup lang="ts">
import { getId } from "@/helpers/ObjectHelpers";

const props = defineProps<{
  type: string;
  value?: number;
  label: string;
  maxLabel: string;
}>();

const link = `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${props.type}`;
const level = props.value ? Math.min(Math.floor(props.value * 10) + 1, 10) : 10;
const image = `/images/icons/${props.type}-${level}.png`;
const tip = `This song has a ${props.label} of ${props.value?.toFixed(2)} where 1.00 is the ${props.maxLabel}.`;
const computedId = getId();
</script>

<template>
  <a v-if="value" :id="computedId" :href="link" target="_blank">
    <img :src="image" width="25" height="25" />
    <BTooltip :target="computedId" triggers="hover click blur" placement="left" :title="tip" />
  </a>
</template>
