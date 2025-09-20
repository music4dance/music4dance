<script setup lang="ts">
import { SongFilter } from "@/models/SongFilter";

const props = defineProps<{
  id: string;
  title?: string;
  sortTip?: string;
  currentTip?: string;
  enableSort?: boolean;
  customSort?: boolean;
  filter?: SongFilter;
}>();

const emit = defineEmits<{
  click: [value: MouseEvent];
}>();

import { computed } from "vue";

const filter = computed(() => props.filter ?? new SongFilter());
const sort = computed(() => filter.value.sort);

const link = computed(() =>
  props.enableSort && !props.customSort ? filter.value.changeSort(props.id).url : "#",
);
const content = computed(() => props.title ?? props.id);
const computedSortTip = computed(() =>
  props.enableSort ? (props.sortTip ?? "Click to sort") : undefined,
);
</script>

<template>
  <span>
    <BLink
      :id="id"
      :href="link"
      underline-variant="light"
      @click="
        console.log('click');
        emit('click', $event);
      "
    >
      <slot>{{ content }}</slot>
      <SortIcon
        v-if="sort.id === id || (id == 'Order' && sort.isChronological)"
        :type="sort.type"
        :direction="sort.direction"
      />
    </BLink>
    <BTooltip v-if="computedSortTip || currentTip" :target="id" placement="top">
      {{ computedSortTip }} <br v-if="computedSortTip && currentTip" />
      {{ currentTip }}
    </BTooltip>
  </span>
</template>
