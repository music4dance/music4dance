<script setup lang="ts">
import { PurchaseInfo } from "@/models/Purchase";
import { computed } from "vue";

const props = defineProps<{
  info: PurchaseInfo;
  isCharm?: boolean;
  styles?: string;
}>();

const width = computed(() => (props.isCharm ? 110 : 32));
const height = computed(() => (props.isCharm ? 40 : 32));
const image = computed(() => (props.isCharm ? props.info.charm : props.info.logo));
</script>

<template>
  <a :href="info.link" target="blank" :style="styles">
    <img :src="image" :width="width" :height="height" :alt="info.altText" />
  </a>
</template>
