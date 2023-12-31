<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import { type BreadCrumbItem, danceTrail } from "@/models/BreadCrumbItem";

const breadcrumbs: BreadCrumbItem[] = [...danceTrail, { text: "Country", active: true }];
</script>

<template>
  <PageFrame id="app" title="Country Western Dance Music" :breadcrumbs="breadcrumbs">
    <p>
      Here at music4dance, we constantly seek ways to serve the broader dance community. We're
      considering adding more Country Western dances to our catalog, but we aren't experts in those
      dances. So please <a href="https://music4dance.blog/feedback/">get in touch with us</a> if
      you're a Country Western dancer and would like to see this site provide support for those
      dances in a way similar to how we support Ballroom dances, including a
      <a href="https://www.music4dance.net/dances/ballroom-competition-categories"
        >competition tempo page</a
      >, pages for each of the dances (as we currently have for
      <a href="https://www.music4dance.net/dances/country-two-step">Country Two Step</a>) and
      include those dances in the
      <a href="https://www.music4dance.net/home/counter">tempo counter</a> and
      <a href="https://www.music4dance.net/home/tempi">tempo list</a> pages.
    </p>
    <p>
      We'd especially <a href="https://music4dance.blog/feedback/">like to hear from you</a> if you
      are an expert in Country Western dances and would be willing to help us build these pages.
      We're looking to start from the tempo guidance the
      <a
        href="https://ucwdc.org/wp-content/uploads/2023/01/UCWDC-ProAm-ProPro-and-Couples-Competition-Music-BPMs-2023-2025.pdf"
        >United Country Western Dance Council</a
      >
      and the
      <a href="https://www1.worldcdf.com/pdf/RULES_BOOK.pdf">World Country Dance Federation</a>
      provide, but would be happy to have your input as well.
    </p>
  </PageFrame>
</template>
