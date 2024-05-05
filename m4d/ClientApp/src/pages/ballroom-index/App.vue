<script setup lang="ts">
import BallroomList from "@/components/BallroomList.vue";
import BlogTagLink from "@/components/BlogTagLink.vue";
import CompetitionCategoryTable from "@/components/CompetitionCategoryTable.vue";
import PageFrame from "@/components/PageFrame.vue";
import TempiLink from "@/components/TempiLink.vue";
import { type BreadCrumbItem, danceTrail } from "@/models/BreadCrumbItem";
import { CompetitionCategory, CompetitionGroup } from "@/models/Competition";
import { TypedJSON } from "typedjson";

declare const model_: string;
const group = TypedJSON.parse(model_, CompetitionGroup)!;
const breadcrumbs: BreadCrumbItem[] = [...danceTrail, { text: "Ballroom", active: true }];

function categoryLink(category: CompetitionCategory): string {
  return `/dances/${category.canonicalName}`;
}
</script>

<template>
  <PageFrame id="app" title="Competition Ballroom Dancing" :breadcrumbs="breadcrumbs">
    <BallroomList>
      One of the
      <a
        href="https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/"
        target="_blank"
      >
        core ideas</a
      >
      behind <a href="https://www.music4dance.net">music4dance</a> is to find interesting music to
      build playlists for competitions rounds. This page is a central location to start a search for
      songs for your own playlists based on the tempo definitions of competition ballroom dance
      established by
      <a href="http://www.worlddancesport.org/Rule/Athlete/Competition" target="_blank"
        >World Dance Council</a
      >
      and
      <a href="https://www.ndca.org/pages/ndca_rule_book/Default.asp" target="_blank"
        >National Dance Council of America</a
      >
      and our database of songs.
    </BallroomList>
    <div v-for="category in group.categories" :key="category.name">
      <h2>
        <a :href="categoryLink(category)">{{ category.name }}</a>
      </h2>
      <CompetitionCategoryTable
        :dances="category.round"
        :title="category.fullRoundTitle"
        :use-full-name="false"
      ></CompetitionCategoryTable>
    </div>
    <div>
      <TempiLink />
      <BlogTagLink title="Ballroom" tag="ballroom"></BlogTagLink>
    </div>
  </PageFrame>
</template>
