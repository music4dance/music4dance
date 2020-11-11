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
    <b-tabs>
      <b-tab title="By Name" active>
        <b-list-group>
          <b-list-group-item
            v-for="dance in sortedDances"
            :key="dance.danceId"
            button
            :active="danceId === dance.danceId"
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
            @click="choose(dance.danceId)"
          >
            {{ dance.danceName }}
          </b-list-group-item>
        </b-list-group>
      </b-tab>
    </b-tabs>
  </b-modal>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats } from "@/model/DanceStats";

declare const environment: DanceEnvironment;

@Component
export default class DanceChooser extends Vue {
  @Prop() private readonly danceId!: string;

  private get sortedDances(): DanceStats[] {
    return environment
      ? environment.flatStats
          .filter((d) => d.songCount > 0)
          .sort((a, b) => a.danceName.localeCompare(b.danceName))
      : [];
  }

  private get groupedDances(): DanceStats[] {
    return environment
      ? environment.groupedStats.filter((d) => d.songCount > 0)
      : [];
  }

  private choose(danceId?: string): void {
    this.$emit("chooseDance", danceId);
  }

  private groupVariant(dance: DanceStats): string | undefined {
    return dance.isGroup && !(this.danceId === dance.danceId)
      ? "dark"
      : undefined;
  }
}
</script>

<style scoped lang="scss">
.sub-item {
  padding-left: 2em;
}
</style>
