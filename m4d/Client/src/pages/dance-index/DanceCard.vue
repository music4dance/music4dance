<template>
  <b-card
    no-body
    border-variant="primary"
    style="margin-bottom:1rem;min-width:15rem"
  >
    <b-card-header header-bg-variant="primary" header-text-variant="white">
      <dance-item :dance="group" variant="secondary" text-style="color:white"></dance-item>
    </b-card-header>

    <b-list-group flush>
      <b-list-group-item v-for="dance in dances" :key="dance.danceId">
        <dance-item :dance="dance" variant="primary"></dance-item>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import { DanceInstance, Meter, TempoRange, DanceStats } from '@/model/DanceStats';
import DanceItem from './DanceItem.vue';

@Component({
  components: {
    DanceItem,
  },
})
export default class DanceCard extends Vue {
    @Prop() private group!: DanceStats;

    private get dances(): DanceStats[] {
      return this.group.children.filter((d) => d.songCount > 0);
    }
}
</script>
