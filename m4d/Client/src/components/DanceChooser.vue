<template>
  <b-modal
    id="danceChooser"
    header-bg-variant="primary"
    header-text-variant="light"
    hide-footer
  >
    <template v-slot:modal-title>
      <b-icon-award></b-icon-award>&nbsp;Choose Dance Style
    </template>
    <b-button
      block
      v-if="danceId"
      variant="outline-primary"
      @click="choose()"
      style="margin-bottom: 0.5em"
    >
      Search All Dance Styles
    </b-button>
    <b-input-group class="mb-2">
      <b-form-input
        type="text"
        v-model="nameFilter"
        placeholder="Filter Dances"
        autofocus
      ></b-form-input>
      <b-input-group-append is-text
        ><b-icon-search></b-icon-search
      ></b-input-group-append>
    </b-input-group>
    <b-tabs>
      <b-tab title="By Name" active>
        <b-list-group>
          <b-list-group-item
            v-for="dance in sortedDances"
            :key="dance.danceId"
            button
            :active="danceId === dance.danceId"
            :disabled="exists(dance.danceId)"
            @click="choose(dance.danceId)"
          >
            {{ dance.danceName }}
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Style">
        <b-list-group>
          <b-list-group-item
            v-for="dance in groupedDances"
            :key="dance.danceId"
            button
            :variant="groupVariant(dance)"
            :class="{ 'sub-item': !dance.isGroup }"
            :active="danceId === dance.danceId"
            :disabled="exists(dance.danceId)"
            @click="choose(dance.danceId)"
          >
            {{ dance.danceName }}
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Tempo" v-if="hasTempo">
        <dance-list
          :dances="dances"
          :beatsPerMinute="tempo"
          :beatsPerMeasure="numerator"
          :epsilonPercent="20"
          :filter="nameFilter"
          countMethod="beats"
          @choose-dance="choose($event)"
        ></dance-list>
      </b-tab>
    </b-tabs>
  </b-modal>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Mixins } from "vue-property-decorator";
import { DanceStats } from "@/model/DanceStats";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import DanceList from "@/pages/tempo-counter/components/DanceList.vue";

@Component({
  components: {
    DanceList,
  },
})
export default class DanceChooser extends Mixins(EnvironmentManager) {
  @Prop() private readonly danceId!: string;
  @Prop() private readonly filterIds?: string[];
  @Prop() private readonly tempo?: number;
  @Prop() private readonly numerator?: number;

  private readonly nameFilter: string = "";

  private get sortedDances(): DanceStats[] {
    const environment = this.environment;
    return environment
      ? this.filterAll(environment.flatStats).sort((a, b) =>
          a.danceName.localeCompare(b.danceName)
        )
      : [];
  }

  private get groupedDances(): DanceStats[] {
    const environment = this.environment;
    return environment ? this.filterAll(environment.groupedStats, true) : [];
  }

  private get dances(): DanceStats[] {
    const environment = this.environment;
    return environment && environment.stats ? environment.stats : [];
  }

  private exists(danceId: string): boolean {
    const filtered = this.filterIds;
    if (!filtered) {
      return false;
    }
    return !!filtered.find((id) => id === danceId);
  }

  private choose(danceId?: string): void {
    this.$emit("chooseDance", danceId);
  }

  private groupVariant(dance: DanceStats): string | undefined {
    return dance.isGroup && !(this.danceId === dance.danceId)
      ? "dark"
      : undefined;
  }

  private get hasTempo(): boolean {
    return !!this.tempo && !!this.numerator;
  }

  private filterAll(
    dances: DanceStats[],
    includeChildren = false
  ): DanceStats[] {
    const filter = this.nameFilter;
    return dances.filter(
      (d) =>
        d.songCount > 0 &&
        (!filter ||
          d.danceName.toLowerCase().indexOf(filter) !== -1 ||
          (includeChildren &&
            d.isGroup &&
            d.children.find(
              (c) =>
                c.songCount > 0 &&
                c.danceName.toLowerCase().indexOf(filter) !== -1
            )))
    );
  }
}
</script>

<style lang="scss" scoped>
.sub-item {
  padding-left: 2em;
}
</style>
