<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";
import { SortOrder } from "@/models/SongSort";
import { computed } from "vue";

const props = defineProps<{
  userName: string;
  displayName: string;
  text: string;
  type: string;
  include: boolean;
  count: number;
}>();

const url = computed(() => {
  const filter = new SongFilter();
  filter.user = `${props.include ? "+" : "-"}${props.userName}|${props.type}`;
  filter.sortOrder = SortOrder.Modified;
  return `/song/filterSearch?filter=${filter.encodedQuery}`;
});

const formattedText = computed(() => {
  return props.text.replace("{{ userName }}", props.displayName);
});
</script>

<template>
  <li class="list-clean-aligned lh-lg">
    <IBiPatchPlus v-if="props.include" />
    <IBiPatchMinus v-else />
    <span class="px-1" />
    <IBiHeartFill v-if="type === 'l'" class="text-danger" />
    <span v-else-if="type == 'h'" class="position-relative">
      <IBiHeartFill class="" />
      <IBiXCircle class="text-danger position-absolute bottom-0 start-0 text-secondary" />
    </span>
    <IBiPencil v-else />
    <span class="px-1" />
    <a :href="url" class="ms-1" v-html="formattedText" />
    <span v-if="include"> ({{ count }})</span>
  </li>
</template>

<style scoped>
.list-clean-aligned {
  list-style-type: none;
  margin-left: -2em;
}
</style>
