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
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongFilter } from "@/model/SongFilter";

@Component
export default class SortableHeader extends Vue {
  @Prop() private readonly id!: string;
  @Prop() private readonly title?: string;
  @Prop() private readonly tip!: string;
  @Prop() private readonly enableSort!: boolean;
  @Prop() private readonly filter!: SongFilter;

  private get sortLink(): string {
    return this.filter.changeSort(this.id).url;
  }

  private get content(): string {
    return this.title ?? this.id;
  }

  private get sortIcon(): string | undefined {
    const sort = this.filter.sort;
    if (sort.order !== this.id) {
      return undefined;
    }
    const type = sort.type;
    const direction = sort.direction === "asc" ? "down" : "down-alt";
    return type ? `sort-${type}-${direction}` : `sort-${direction}`;
  }

  // private tipMessage(): string {
  //     return this.tip ?? 'Empty ToolTip';
  // }
}
</script>
