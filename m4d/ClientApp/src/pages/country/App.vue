<script setup lang="ts">
import { type BreadCrumbItem, danceTrail } from "@/models/BreadCrumbItem";
import { CompetitionCategory, CompetitionGroup } from "@/models/Competition";
import { TypedJSON } from "typedjson";
import { computed, ref } from "vue";

declare const model_: string;
const group = TypedJSON.parse(model_, CompetitionGroup)!;
const breadcrumbs: BreadCrumbItem[] = [...danceTrail, { text: "Country", active: true }];

// Since Country only has one category, we can get it directly
const category = computed(() => group?.categories[0] ?? new CompetitionCategory());

const showBpm = ref(true);
</script>

<template>
  <PageFrame id="app" title="Country Western Competition Dancing" :breadcrumbs="breadcrumbs">
    <CompetitionDanceList organization-type="country">
      One of the
      <a
        href="https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/"
        target="_blank"
      >
        core ideas</a
      >
      behind <a href="https://www.music4dance.net">music4dance</a> is to find interesting music to
      build playlists for competitions rounds. This page showcases the Country Western competition
      dances with tempo definitions established by the
      <a
        href="https://ucwdc.org/wp-content/uploads/2023/01/UCWDC-ProAm-ProPro-and-Couples-Competition-Music-BPMs-2023-2025.pdf"
        target="_blank"
        >United Country Western Dance Council (UCWDC)</a
      >,
      <a href="https://www1.worldcdf.com/pdf/RULES_BOOK.pdf" target="_blank"
        >World Country Dance Federation (WORLDCDF)</a
      >, and
      <a
        href="https://danceacda.com/wp-content/uploads/2025/08/2025-ACDA-Rules-rev-041325.pdf"
        target="_blank"
        >American Country Dance Association (ACDA)</a
      >.
    </CompetitionDanceList>

    <h2>{{ category.name }}</h2>
    <CompetitionCategoryTable
      v-model:show-bpm="showBpm"
      :dances="category.round"
      :title="category.fullRoundTitle"
      :use-full-name="false"
    />

    <div class="mt-3">
      <p>
        We've implemented Country Western competition dances with tempo ranges from major
        organizations including <a href="https://ucwdc.org/" target="_blank">UCWDC</a>,
        <a href="https://www.worldcdf.com/" target="_blank">WORLDCDF</a>, and
        <a href="https://danceacda.com/" target="_blank">ACDA</a>. Each dance includes the
        organization-specific tempo requirements to help you find the perfect practice music.
      </p>
      <p>
        For more information on individual country dances, click on any dance name in the table
        above to see our detailed information page, including a top ten list of songs at the right
        tempo. You can also explore our
        <a href="/home/counter">tempo counter</a> and <a href="/home/tempi">tempo list</a> pages to
        find songs that match your practice needs.
      </p>
      <p>
        We do not have any in-house expertise with Country Western dance, so we are working solely
        from the resources provided by the organizations mentioned above and assumptions that
        aspects of dance competition are similar between Ballroom and Country competitions. Any
        errors introduced are ours, not those of the organizations mentioned. If you have
        suggestions or feedback on our Country Western dance catalog, please
        <a href="https://music4dance.blog/feedback/">get in touch with us</a>. We're always looking
        to improve and expand our offerings for the dance community.
      </p>
      <p>
        We've had over ten years building our Ballroom and Social dance catalogs, so we're excited
        to apply that experience to our Country Western dance offerings.
      </p>
    </div>

    <div class="mt-3">
      <TempiLink />
      <BlogTagLink title="Country" tag="country-dance" />
    </div>
  </PageFrame>
</template>
