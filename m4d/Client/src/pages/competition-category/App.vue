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
      Other categories of <a :href="groupLink">{{groupTitle}}</a> are:
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
import { Component, Vue } from 'vue-property-decorator';
import Page from '@/components/Page.vue';
import BallroomList from '@/components/BallroomList.vue';
import LinkCategory from '@/components/LinkCategory.vue';
import TempiLink from '@/components/TempiLink.vue';
import BlogTagLink from '@/components/BlogTagLink.vue';
import CompetitionCategoryTable from '@/components/CompetitionCategoryTable.vue';
import { CompetitionGroupModel, CompetitionCategory } from '@/model/Competition';
import { TypedJSON } from 'typedjson';
import { BreadCrumbItem, ballroomTrail } from '@/model/BreadCrumbItem';

declare const model: string;

@Component({
  components: {
    BallroomList,
    BlogTagLink,
    CompetitionCategoryTable,
    LinkCategory,
    Page,
    TempiLink,
  },
})
export default class App extends Vue {
  private groupModel: CompetitionGroupModel;
  private breadcrumbs: BreadCrumbItem[];

  constructor() {
    super();

    const serializer = new TypedJSON(CompetitionGroupModel);
    const modelS = JSON.stringify(model);
    const modelT = serializer.parse(modelS);
    if (!modelT) {
      throw new Error('Unable to parse model');
    }
    this.groupModel = modelT;
    this.breadcrumbs = [
      ...ballroomTrail,
      { text: this.category.name, active: true},
    ];
  }

  private get category(): CompetitionCategory {
    return this.groupModel.currentCategory;
  }

  private get groupTitle(): string {
    return `competition ${this.groupModel.group.name.toLowerCase()} dancing`;
  }

  private get groupLink(): string {
    return `/dances/${this.groupModel.group.name.toLowerCase()}-competition-categories`;
  }
}
</script>
