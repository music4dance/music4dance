<template>
  <page
    id="app"
    title="Competition Ballroom Dancing"
    :breadcrumbs="breadcrumbs"
  >
    <ballroom-list>
      One of the
      <a
        href="https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/"
      >
        core ideas</a
      >
      behind <a href="https://www.music4dance.net">music4dance</a> is to find
      interesting music to build playlists for competitions rounds. This page is
      a central location to start a search for songs for your own playlists
      based on the tempo definitions of competition ballroom dance established
      by
      <a href="http://www.worlddancesport.org/Rule/Athlete/Competition"
        >World Dance Council</a
      >
      and
      <a href="https://www.ndca.org/pages/ndca_rule_book/Default.asp"
        >National Dance Council of America</a
      >
      and our database of songs.
    </ballroom-list>
    <div v-for="category in group.categories" :key="category.name">
      <h2>
        <a :href="categoryLink(category)">{{ category.name }}</a>
      </h2>
      <competition-category-table
        :dances="category.round"
        :title="category.fullRoundTitle"
      ></competition-category-table>
    </div>
    <dl>
      <tempi-link></tempi-link>
      <blog-tag-link title="Ballroom" tag="ballroom"></blog-tag-link>
    </dl>
  </page>
</template>

<script lang="ts">
import BallroomList from "@/components/BallroomList.vue";
import BlogTagLink from "@/components/BlogTagLink.vue";
import CompetitionCategoryTable from "@/components/CompetitionCategoryTable.vue";
import Page from "@/components/Page.vue";
import TempiLink from "@/components/TempiLink.vue";
import { BreadCrumbItem, danceTrail } from "@/model/BreadCrumbItem";
import { CompetitionCategory, CompetitionGroup } from "@/model/Competition";
import { TypedJSON } from "typedjson";
import Vue from "vue";

declare const model: string;

export default Vue.extend({
  components: {
    BallroomList,
    BlogTagLink,
    CompetitionCategoryTable,
    Page,
    TempiLink,
  },
  data() {
    const modelT = TypedJSON.parse(model, CompetitionGroup);
    if (!modelT) {
      throw new Error("Unable to parse model");
    }
    return new (class {
      group: CompetitionGroup = modelT!;
      breadcrumbs: BreadCrumbItem[] = [
        ...danceTrail,
        { text: "Ballroom", active: true },
      ];
    })();
  },
  methods: {
    categoryLink(category: CompetitionCategory): string {
      return `/dances/${category.canonicalName}`;
    },
  },
});
</script>
