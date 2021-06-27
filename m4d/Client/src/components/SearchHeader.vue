<template>
  <div>
    <h1>Search Results</h1>
    <h3>
      {{ filter.description }}
      <b-button :href="changeLink" variant="primary" class="mx-1"
        >Change</b-button
      >
      <b-button v-if="playListRef" :href="playListRef" class="mx-1"
        >Create Spotify PlayList</b-button
      >
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

  private get playListRef(): string | undefined {
    return this.filter.getPlayListRef(this.user);
  }
}
</script>
