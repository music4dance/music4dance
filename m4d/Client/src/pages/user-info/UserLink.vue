<template>
  <li class="list-clean">
    <b-icon :icon="valenceIcon" class="mr-1"></b-icon>
    <b-icon-heart-fill v-if="type === 'l'" variant="danger"></b-icon-heart-fill>
    <b-iconstack v-else-if="type === 'h'">
      <b-icon-heart
        stacked
        variant="secondary"
        scale="0.75"
        shift-v="-1"
      ></b-icon-heart>
      <b-icon-x-circle stacked variant="danger"></b-icon-x-circle>
    </b-iconstack>
    <b-icon-pencil v-else></b-icon-pencil>
    <a :href="url" v-html="formattedText" class="ml-1"></a>
    <span v-if="include"> ({{ count }})</span>
  </li>
</template>

<script lang="ts">
import { SongFilter } from "@/model/SongFilter";
import { SortOrder } from "@/model/SongSort";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({})
export default class UserLink extends Vue {
  @Prop() private userName!: string;
  @Prop() private displayName!: string;
  @Prop() private text!: string;
  @Prop() private type!: string;
  @Prop() private include!: boolean;
  @Prop() private count!: boolean;

  private get url(): string {
    const filter = new SongFilter();
    filter.user = `${this.include ? "+" : "-"}${this.userName}|${this.type}`;
    filter.sortOrder = SortOrder.Modified;
    return `/song/filterSearch?filter=${filter.encodedQuery}`;
  }

  private get valenceIcon(): string {
    return this.include ? "patch-plus" : "patch-minus";
  }

  private get formattedText(): string {
    return this.text.replace("{{ userName }}", this.displayName);
  }
}
</script>
