<template>
  <b-list-group :flush="flush">
    <b-list-group-item
      v-for="(dance, idx) in dances"
      :key="idx"
      :variant="danceVariant(dance)"
    >
      <dance-item :dance="dance" :showTempo="showTempo"></dance-item>
    </b-list-group-item>
  </b-list-group>
</template>

<script lang="ts">
import DanceItem from "@/components/DanceItem.vue";
import { DanceStats } from "@/model/DanceStats";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: { DanceItem } })
export default class DanceList extends Vue {
  @Prop() private dances!: DanceStats[];
  @Prop() private flush?: boolean;
  @Prop() private showTempo?: boolean;

  private get filteredDances(): DanceStats[] {
    return this.dances.filter((d) => d.songCount > 0);
  }

  private danceVariant(dance: DanceStats): string {
    return dance.isGroup ? "primary" : "light";
  }
}
</script>
