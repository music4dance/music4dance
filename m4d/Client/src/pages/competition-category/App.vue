<template>
  <page id="app" :title="category.name" :breadcrumbs="breadcrumbs">
    <ballroom-list :name="category.name"></ballroom-list>
    <competition-category-table
      :dances="category.round"
      :title="category.fullRoundTitle"
    ></competition-category-table>
    <competition-category-table
      v-if="category.extras"
      :dances="category.extras"
      :title="category.extraDancesTitle"
    ></competition-category-table>
    <div>
      Other categories of <a :href="groupLink">{{ groupTitle }}</a> are:
      <ul>
        <li v-for="category in groupModel.otherCategories" :key="category.name">
          <link-category :name="category.name"></link-category>
        </li>
      </ul>
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
import LinkCategory from "@/components/LinkCategory.vue";
import Page from "@/components/Page.vue";
import TempiLink from "@/components/TempiLink.vue";
import { ballroomTrail, BreadCrumbItem } from "@/model/BreadCrumbItem";
import {
  CompetitionCategory,
  CompetitionGroupModel,
} from "@/model/Competition";
import { TypedJSON } from "typedjson";
import Vue from "vue";

declare const model: string;

export default Vue.extend({
  components: {
    BallroomList,
    BlogTagLink,
    CompetitionCategoryTable,
    LinkCategory,
    Page,
    TempiLink,
  },
  data() {
    const modelT = TypedJSON.parse(model, CompetitionGroupModel);
    if (!modelT) {
      throw new Error("Unable to parse model");
    }
    const modelTT = modelT;
    return new (class {
      groupModel: CompetitionGroupModel = modelTT;
      breadcrumbs: BreadCrumbItem[] = [
        ...ballroomTrail,
        { text: modelTT.currentCategory.name, active: true },
      ];
    })();
  },
  computed: {
    category(): CompetitionCategory {
      return this.groupModel.currentCategory;
    },
    groupTitle(): string {
      return `competition ${this.groupModel.group.name.toLowerCase()} dancing`;
    },
    groupLink(): string {
      return `/dances/${this.groupModel.group.name.toLowerCase()}-competition-categories`;
    },
  },
});
</script>
