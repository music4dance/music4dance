<template>
  <b-row>
    <b-col md="8">
      <b-pagination-nav
        :link-gen="linkGen"
        :number-of-pages="pageCount"
        v-model="pageNumber"
        limit="9"
        first-number
        last-number
      ></b-pagination-nav>
    </b-col>
    <b-col md="2"
      >Page {{ pageNumber }} of {{ pageCount }} ({{ count }} songs found)</b-col
    >
    <b-col md="2"><a href="/song/advancedsearchform">New Search</a></b-col>
  </b-row>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { SongFilter } from "@/model/SongFilter";

declare const environment: DanceEnvironment;

@Component
export default class SongFooter extends Vue {
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly count!: number;

  private pageNumber: number;

  constructor() {
    super();
    this.pageNumber = this.filter.page ?? 1;
  }

  private linkGen(pageNum: number): string {
    return `${this.filter.url}&page=${pageNum}`;
  }

  private get pageCount(): number {
    return Math.ceil(this.count / 25);
  }
}
</script>
