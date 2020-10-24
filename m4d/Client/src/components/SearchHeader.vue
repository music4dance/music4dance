<template>
  <div>
    <h1>Search Results</h1>
    <h3>
      {{ filter.description }}
      <b-button :href="changeLink">Change</b-button>
    </h3>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongFilter } from "@/model/SongFilter";

@Component
export default class SearchHeader extends Vue {
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly user?: string;

  private get changeLink(): string {
    return `/song/advancedsearchform?filter=${this.filter.encodedQuery}`;
  }
}
</script>
