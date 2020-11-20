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
import { SongFilter } from "@/model/SongFilter";

@Component
export default class SongFooter extends Vue {
  @Prop() private readonly filter!: SongFilter;
  @Prop() private readonly count!: number;
  @Prop() private readonly href?: string;

  private pageNumber: number;

  constructor() {
    super();
    this.pageNumber = this.filter.page ?? 1;
  }

  private linkGen(pageNum: number): string {
    const href = this.href;
    return href
      ? this.pagedUrl(href, pageNum)
      : this.pagedUrl(this.filter.url, pageNum);
  }

  private get pageCount(): number {
    return Math.max(1, Math.ceil(this.count / 25));
  }

  private pagedUrl(url: string, pageNum: number): string {
    return url.includes("?")
      ? `${url}&page=${pageNum}`
      : `${url}?page=${pageNum}`;
  }
}
</script>
