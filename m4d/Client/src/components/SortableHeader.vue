<template>
  <span>
    <span v-if="enableSort">
      <a :href="sortLink" :id="id">
        <slot>{{ content }}</slot>
        <b-icon v-if="sortIcon" :icon="sortIcon"></b-icon>
      </a>
      <b-tooltip
        v-if="tip"
        :target="id"
        triggers="hover click blur"
        placement="left"
      >
        {{ tip }}
      </b-tooltip>
    </span>
    <span v-else
      ><slot>{{ content }}</slot></span
    >
  </span>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: {},
  props: {
    id: { type: String, required: true },
    title: String,
    tip: String,
    enableSort: Boolean,
    filter: Object as PropType<SongFilter>,
  },
  computed: {
    sortLink(): string {
      return this.filter.changeSort(this.id).url;
    },

    content(): string {
      return this.title ?? this.id;
    },

    sortIcon(): string | undefined {
      const sort = this.filter.sort;
      if (sort.order !== this.id) {
        return undefined;
      }
      const type = sort.type;
      const direction = sort.direction === "asc" ? "down" : "down-alt";
      return type ? `sort-${type}-${direction}` : `sort-${direction}`;
    },
  },
});
</script>
