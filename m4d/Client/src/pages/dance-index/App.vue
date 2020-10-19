<template>
  <page id="app" title="Dance Styles" :breadcrumbs="breadcrumbs">
    <b-card-group columns>
        <dance-card v-for="group in orderedGroups" :key="group.danceId" :group="group"></dance-card>
      </b-card-group>
    <p id="competition">
      Or check out our more traditional ballroom competition categories including
      <a href="/dances/international-standard">International Standard</a>, 
      <a href="/dances/international-latin">International Latin</a>,
      <a href="/dances/american-smooth">American Smooth</a> and 
      <a href="/dances/american-rhythm">American Rhythm</a>
      by going to our <a href="/dances/ballroom-competition-categories">Ballroom competition categories</a> page.
    </p>
    <p id="wedding">
      We also have songs cross-indexed by style of dance and type of wedding event on our 
      <a href="/dances/wedding-music">Wedding Dance Music</a> page.
    </p>
    <p>
      Choose from the dances styles to get more information or click on the number next to the name 
      to get a list of songs that are suitable to dance that dance to.
    </p>
    <p>
      Don't agree with how we've organized dance styles? Well, we're not convinced there is a "right way"
      but we're doing some research to see if we can find a better way (or maybe multiple ways).
       Please help us out by <a href="https://music4dance.blog/feedback/">sending us feedback</a> so
       that we can include your ideas about how to organize dance styles in the future.
    </p>
  </page>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Vue } from 'vue-property-decorator';
import Page from '@/components/Page.vue';
import DanceCard from './DanceCard.vue';
import { TypedJSON } from 'typedjson';
import { DanceStats } from '@/model/DanceStats';
import { BreadCrumbItem, danceCrumb, homeCrumb } from '@/model/BreadCrumbItem';

declare const model: string;

@Component({
  components: {
    DanceCard,
    Page,
  },
})
export default class App extends Vue {
  private groups: DanceStats[];
  private order: string[] = ['LTN', 'WLZ', 'SWG', 'FXT', 'TNG', 'MSC', 'PRF'];
  private breadcrumbs: BreadCrumbItem[] = [
    homeCrumb,
    { text: 'Dances', active: true},
  ];

constructor() {
    super();

    this.groups = TypedJSON.parseAsArray(model, DanceStats);
  }

  private get orderedGroups(): DanceStats[] {
    return this.order.map((id) => this.groups.find((g) => g.danceId === id)!);
  }
}
</script>
