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
      <b-tab title="By Name" :active="!hasTempo">
        <b-list-group>
          <b-list-group-item
            v-for="dance in sortedDances"
            :key="dance.id"
            button
            :active="danceId === dance.id"
            :disabled="exists(dance.id)"
            @click="chooseEvent(dance.id, $event)"
          >
            <dance-name
              :dance="dance"
              :showSynonyms="true"
              :showTempo="tempoType"
              :hideLink="hideNameLink"
            ></dance-name>
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Style">
        <b-list-group>
          <b-list-group-item
            v-for="(dance, idx) in groupedDances"
            :key="idx"
            button
            :variant="groupVariant(dance)"
            :class="{ item: dance.isGroup, 'sub-item': !dance.isGroup }"
            :active="danceId === dance.id"
            :disabled="exists(dance.id) || (dance.isGroup && !includeGroups)"
            @click="chooseEvent(dance.id, $event)"
          >
            <dance-name
              :dance="dance"
              :showSynonyms="true"
              :showTempo="tempoType"
              :hideLink="hideNameLink"
            ></dance-name>
          </b-list-group-item>
        </b-list-group>
      </b-tab>
      <b-tab title="By Tempo" active v-if="hasTempo">
        <dance-list
          :dances="danceTypes"
          :beatsPerMinute="tempo"
          :beatsPerMeasure="numerator"
          :epsilonPercent="20"
          :filter="nameFilter"
          :hideNameLink="true"
          @choose-dance="hideNameLink"
        ></dance-list>
      </b-tab>
    </b-tabs>
  </b-modal>
</template>

<script lang="ts">
import DanceName from "@/components/DanceName.vue";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats } from "@/model/DanceStats";
import { TempoType } from "@/model/TempoType";
import { TypeStats } from "@/model/TypeStats";
import DanceList from "@/pages/tempo-counter/components/DanceList.vue";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({
  components: {
    DanceName,
    DanceList,
  },
})
export default class DanceChooser extends Mixins(EnvironmentManager) {
  @Prop() private readonly danceId!: string;
  @Prop() private readonly filterIds?: string[];
  @Prop() private readonly tempo?: number;
  @Prop() private readonly numerator?: number;
  @Prop() private readonly includeGroups?: boolean;
  @Prop() private readonly hideNameLink?: boolean;

  private readonly nameFilter: string = "";

  private get sortedDances(): DanceStats[] {
    const environment = this.environment;
    const includeGroups = this.includeGroups;

    return environment
      ? this.filterAll(environment.flatStats)
          .filter((d) => includeGroups || !d.isGroup)
          .sort((a, b) => a.name.localeCompare(b.name))
      : [];
  }

  private get groupedDances(): DanceStats[] {
    const environment = this.environment;
    return environment ? this.filterAll(environment.groupedStats, true) : [];
  }

  private get dances(): DanceStats[] {
    const environment = this.environment;
    return environment && environment.tree ? environment.tree : [];
  }

  private get danceTypes(): TypeStats[] {
    const environment = this.environment;
    return environment && environment.dances ? environment.dances : [];
  }

  private get tempoType(): TempoType {
    return TempoType.Measures;
  }
  private exists(danceId: string): boolean {
    const filtered = this.filterIds;
    if (!filtered) {
      return false;
    }
    return !!filtered.find((id) => id === danceId);
  }

  private chooseEvent(id?: string, event?: MouseEvent): void {
    this.choose(id, event?.ctrlKey);
  }

  private choose(id?: string, persist?: boolean): void {
    this.$emit("choose-dance", id, persist);
  }

  private groupVariant(dance: DanceStats): string | undefined {
    return dance.isGroup && !(this.danceId === dance.id)
      ? "primary"
      : undefined;
  }

  private get hasTempo(): boolean {
    return !!this.tempo && !!this.numerator;
  }

  private filterAll(
    dances: DanceStats[],
    includeChildren = false
  ): DanceStats[] {
    return DanceEnvironment.filterByName(
      dances,
      this.nameFilter,
      includeChildren
    );
  }
}
</script>

<style lang="scss" scoped>
.sub-item {
  padding-left: 2em;
}
.item {
  font-weight: bolder;
}
</style>
