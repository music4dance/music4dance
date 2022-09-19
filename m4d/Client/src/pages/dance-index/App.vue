<template>
  <page
    id="app"
    title="Dance Styles"
    :breadcrumbs="breadcrumbs"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <dance-table :dances="dances"></dance-table>
    <h2 class="mt-2">Other Resources:</h2>
    <p id="competition">
      Check out our more traditional ballroom competition categories including
      <a href="/dances/international-standard">International Standard</a>,
      <a href="/dances/international-latin">International Latin</a>,
      <a href="/dances/american-smooth">American Smooth</a> and
      <a href="/dances/american-rhythm">American Rhythm</a>
      by going to our
      <a href="/dances/ballroom-competition-categories"
        >Ballroom competition categories</a
      >
      page.
    </p>
    <p id="wedding">
      We also have songs cross-indexed by style of dance and type of wedding
      event on our
      <a href="/dances/wedding-music">Wedding Dance Music</a> page.
    </p>
    <p id="tempi">
      Our <a href="/home/tempi">Tempi (Tempos)</a> tool shows this same list of
      dances with additional infomration and the ability to filter and sort in
      different ways..
    </p>
    <p>
      Don't agree with how we've organized dance styles? Well, we're not
      convinced there is a "right way" but we're doing some research to see if
      we can find a better way (or maybe multiple ways). Please help us out by
      <a href="https://music4dance.blog/feedback/" target="_blank"
        >sending us feedback</a
      >
      so that we can include your ideas about how to organize dance styles in
      the future.
    </p>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { BreadCrumbItem, homeCrumb } from "@/model/BreadCrumbItem";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { DanceStats } from "@/model/DanceStats";
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import DanceTable from "./DanceTable.vue";

@Component({
  components: {
    DanceTable,
    Page,
  },
})
export default class App extends Vue {
  private dances: DanceStats[] = [];
  private breadcrumbs: BreadCrumbItem[] = [
    homeCrumb,
    { text: "Dances", active: true },
  ];

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.dances = environment.groupedStats;
  }
}
</script>
