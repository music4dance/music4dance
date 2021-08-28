<template>
  <b-list-group :flush="flush">
    <b-list-group-item v-for="dance in dances" :key="dance.id">
      <dance-item
        :dance="dance"
        variant="primary"
        :showTempo="showTempo"
      ></dance-item>
    </b-list-group-item>
  </b-list-group>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { TypeStats, GroupStats } from "@/model/DanceStats";
import DanceItem from "@/components/DanceItem.vue";

@Component({
  components: {
    DanceItem,
  },
})
export default class DanceList extends Vue {
  @Prop() private group!: GroupStats;
  @Prop() private flush?: boolean;
  @Prop() private showTempo?: boolean;

  private get dances(): TypeStats[] {
    return this.group.dances.filter((d) => d.songCount > 0);
  }
}
</script>
