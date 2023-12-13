<script setup lang="ts">
import BallroomList from "@/components/BallroomList.vue";
import BlogTagLink from "@/components/BlogTagLink.vue";
import CompetitionCategoryTable from "@/components/CompetitionCategoryTable.vue";
import LinkCategory from "@/components/LinkCategory.vue";
import PageFrame from "@/components/PageFrame.vue";
import TempiLink from "@/components/TempiLink.vue";
import { ballroomTrail, type BreadCrumbItem } from "@/models/BreadCrumbItem";
import { CompetitionCategory, CompetitionGroupModel } from "@/models/Competition";
import { TypedJSON } from "typedjson";
import { computed } from "vue";

declare const model_: string;
const model = TypedJSON.parse(model_, CompetitionGroupModel)!;
const breadcrumbs: BreadCrumbItem[] = [
  ...ballroomTrail,
  { text: model?.currentCategory.name ?? "Error", active: true },
];

const category = computed(() => {
  console.log("Entering Category");
  const temp = model?.currentCategory ?? new CompetitionCategory();
  return temp;
});

const groupTitle = computed(() => `competition ${model?.group.name.toLowerCase()} dancing`);
const groupLink = computed(
  () => `/dances/${model?.group.name.toLowerCase()}-competition-categories`,
);
</script>

<template>
  <PageFrame id="app" :title="category.name" :breadcrumbs="breadcrumbs">
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
        <li v-for="category in model.otherCategories" :key="category.name">
          <link-category :name="category.name"></link-category>
        </li>
      </ul>
    </div>
    <dl>
      <tempi-link></tempi-link>
      <blog-tag-link title="Ballroom" tag="ballroom"></blog-tag-link>
    </dl>
  </PageFrame>
</template>
