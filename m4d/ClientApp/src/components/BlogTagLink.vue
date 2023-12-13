<script setup lang="ts">
import { toTitleCase } from "@/helpers/StringHelpers";
import { computed } from "vue";
import info from "@/assets/images/icons/info.png";

const props = defineProps<{
  title?: string;
  tag: string;
}>();

const safeTitle = computed(() => {
  return props.title ? props.title : toTitleCase(props.tag);
});

const tagLink = computed(() => {
  return `https://music4dance.blog/tag/${props.tag}`;
});
</script>

<template>
  <div>
    <dd>
      <img :src="info" alt="tools" width="24" height="24" />
      Read the <a href="https://music4dance.blog">music4dance blog</a> entries about
      <a :href="tagLink">{{ safeTitle }}</a
      >.
    </dd>
  </div>
</template>
