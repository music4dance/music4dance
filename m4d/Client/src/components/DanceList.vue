<template>
  <b-list-group :flush="flush">
    <b-list-group-item v-for="dance in dances" :key="dance.danceId">
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
import { DanceStats } from "@/model/DanceStats";
import DanceItem from "@/components/DanceItem.vue";

@Component({
  components: {
    DanceItem,
  },
})
export default class DanceCard extends Vue {
  @Prop() private group!: DanceStats;
  @Prop() private flush?: boolean;
  @Prop() private showTempo?: boolean;

  private get dances(): DanceStats[] {
    return this.group.children.filter((d) => d.songCount > 0);
  }
}
</script>
