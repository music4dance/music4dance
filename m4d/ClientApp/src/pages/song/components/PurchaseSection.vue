<script setup lang="ts">
import { PurchaseInfo } from "@/models/Purchase";
import { SongFilter } from "@/models/SongFilter";

defineProps<{
  purchaseInfos: PurchaseInfo[];
  filter: SongFilter;
}>();
</script>

<template>
  <div>
    <span v-for="pi in purchaseInfos" :key="pi.service" class="mx-auto my-1" style="width: 110px">
      <PurchaseLogo
        :info="pi"
        :is-charm="true"
        styles="margin-right: .25rem; margin-bottom: .25rem"
      ></PurchaseLogo>
    </span>
    <BButton
      v-if="filter && !filter.isEmpty"
      :href="filter.url"
      small
      variant="primary"
      class="my-1"
      ><IBiSearch aria-hidden="true" /> Back to Search</BButton
    >
    <BButton
      variant="outline-primary"
      class="ms-1 my-1"
      small
      href="https://music4dance.blog/music4dance-help/song-details/"
      target="_blank"
      ><IBiQuestionCircle aria-hidden="true" /> Help</BButton
    >
  </div>
</template>
