<script setup lang="ts">
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

const category = computed(() => model?.currentCategory ?? new CompetitionCategory());

const groupTitle = computed(() => `competition ${model?.group.name.toLowerCase()} dancing`);
const groupLink = computed(
  () => `/dances/${model?.group.name.toLowerCase()}-competition-categories`,
);
</script>

<template>
  <PageFrame id="app" :title="category.name" :breadcrumbs="breadcrumbs">
    <BallroomList :name="category.name" />
    <CompetitionCategoryTable
      :dances="category.round"
      :title="category.fullRoundTitle"
      :use-full-name="false"
    />
    <CompetitionCategoryTable
      v-if="category.extras && category.extras.length > 0"
      :dances="category.extras"
      :title="category.extraDancesTitle"
      :use-full-name="false"
    />
    <div>
      Other categories of <a :href="groupLink">{{ groupTitle }}</a> are:
      <ul>
        <li v-for="cat in model.otherCategories" :key="cat.name">
          <LinkCategory :name="cat.name" />
        </li>
      </ul>
    </div>
    <dl>
      <TempiLink />
      <BlogTagLink title="Ballroom" tag="ballroom" />
    </dl>
  </PageFrame>
</template>
