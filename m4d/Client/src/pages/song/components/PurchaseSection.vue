<template>
  <div>
    <span
      v-for="pi in purchaseInfos"
      :key="pi.service"
      class="mx-auto"
      style="width: 110px"
    >
      <purchase-logo
        :info="pi"
        :isCharm="true"
        styles="margin-right: .25rem; margin-bottom: .25rem"
      ></purchase-logo>
    </span>
    <b-button
      v-if="filter && !filter.isEmpty"
      :href="filter.url"
      small
      variant="primary"
      ><b-icon-search aria-hidden="true"></b-icon-search> Back to
      Search</b-button
    >
    <b-button
      variant="outline-primary"
      class="ml-1"
      small
      href="https://music4dance.blog/music4dance-help/song-details/"
      target="_blank"
      ><b-icon-question-circle aria-hidden="true"></b-icon-question-circle>
      Help</b-button
    >
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { PurchaseInfo } from "@/model/Purchase";
import { SongFilter } from "@/model/SongFilter";
import PurchaseLogo from "@/components/PurcahseLogo.vue";

@Component({ components: { PurchaseLogo } })
export default class SongStats extends Vue {
  @Prop() private readonly purchaseInfos!: PurchaseInfo[];
  @Prop() private readonly filter!: SongFilter;
}
</script>
