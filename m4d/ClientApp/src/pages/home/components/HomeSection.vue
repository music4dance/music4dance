<script setup lang="ts">
import FeatureLink from "@/components/FeatureLink.vue";
import { type FeatureInfo } from "@/models/FeatureInfo";
import { type Link } from "@/models/Link";

export interface CardInfo {
  title: Link;
  image: string;
  items: Link[];
}

const props = defineProps<{
  name: string;
  category: string;
  features?: FeatureInfo[];
}>();

const classes: string[] = [props.category];

const image: string = `/images/icons/${props.category}.png`;
</script>

<template>
  <div :id="name" style="margin-top: 1rem">
    <div class="row col" style="margin-bottom: 1rem">
      <span>
        <img :src="image" :alt="name" width="48" height="48" />
        <h2 :class="classes" style="padding-left: 0.5rem; display: inline; vertical-align: center">
          {{ name }}
        </h2>
      </span>
    </div>
    <slot>
      <div v-if="features" class="row col" style="display: block; margin-left: 1rem">
        <FeatureLink v-for="feature in features" :key="feature.title" :info="feature"></FeatureLink>
      </div>
    </slot>
  </div>
</template>
