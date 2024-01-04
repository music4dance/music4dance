<script setup lang="ts">
import type { ColorVariant } from "bootstrap-vue-next";
import { computed } from "vue";

const props = defineProps<{
  title: string;
  type: string;
  link?: string;
  variant: string;
}>();

const typeError = computed(() => {
  return `${props.type} is not a valid type for Feature Button`;
});

const reference = computed(() => {
  if (props.link?.startsWith("https")) {
    return props.link;
  }
  switch (props.type) {
    case "play":
      return `https://www.music4dance.net${props.link}`;
    case "docs":
      return `https://music4dance.blog/music4dance-help${props.link}`;
    case "blog":
      return `https://music4dance.blog${props.link}`;
    default:
      throw new Error(typeError.value);
  }
});

const safeVariant = computed(() => {
  return (props.variant ? props.variant.toLowerCase() : "primary") as ColorVariant;
});
</script>

<template>
  <BButton v-if="link" :variant="safeVariant" size="sm" style="margin-left: 1em" :href="reference">
    <IBiPlay v-if="type == 'play'" /><IBiFileText v-else-if="type == 'docs'" /><IBiPencilSquare
      v-else
    />&nbsp;{{ title }}
  </BButton>
</template>
