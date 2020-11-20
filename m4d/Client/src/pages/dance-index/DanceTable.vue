<template>
  <b-card-group columns>
    <dance-card
      v-for="group in orderedGroups"
      :key="group.danceId"
      :group="group"
    ></dance-card>
  </b-card-group>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import DanceCard from "./DanceCard.vue";

@Component({
  components: {
    DanceCard,
  },
})
export default class DanceTable extends Vue {
  @Prop() private groups!: DanceStats[];
  private order: string[] = ["LTN", "WLZ", "SWG", "FXT", "TNG", "MSC", "PRF"];

  private get orderedGroups(): DanceStats[] {
    return this.order.map((id) => this.groups.find((g) => g.danceId === id)!);
  }
}
</script>
