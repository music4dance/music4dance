<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";

const props = defineProps<{
  id: string;
  title?: string;
  tip?: string;
  enableSort?: boolean;
  filter: SongFilter;
}>();

const filter = props.filter;
const sort = filter.sort;

const sortLink = filter.changeSort(props.id).url;
const content = props.title ?? props.id;
</script>

<template>
  <span>
    <span v-if="enableSort">
      <a :id="id" :href="sortLink">
        <slot>{{ content }}</slot>
        <SortIcon v-if="sort.id === id" :type="sort.type" :direction="sort.direction" />
      </a>
      <BTooltip v-if="tip" :target="id" triggers="hover click blur" placement="left">
        {{ tip }}
      </BTooltip>
    </span>
    <span v-else
      ><slot>{{ content }}</slot></span
    >
  </span>
</template>
