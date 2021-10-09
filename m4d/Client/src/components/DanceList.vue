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
import DanceItem from "@/components/DanceItem.vue";
import { GroupStats } from "@/model/GroupStats";
import { TypeStats } from "@/model/TypeStats";
import { Component, Prop, Vue } from "vue-property-decorator";

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
