<template>
  <div>
    <vue-showdown id="description" :markdown="description"></vue-showdown>
    <div
      id="tempo-info"
      v-if="!dance.isGroup && dance.dance.meter.numerator != 1"
    >
      <h2>Tempo Information</h2>
      <p>
        The {{ danceName }} is generally dance to music in a
        {{ dance.dance.meter.toString() }} meter {{ rangeText }}
        {{ bpmText }} ({{ mpmText }}).
        <a :href="tempoFilter.url">Click here</a> to see a list of
        {{ danceName }} songs {{ rangeText }} {{ mpmText }}.
      </p>
      <tempi-link></tempi-link>
    </div>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Vue, Prop } from "vue-property-decorator";
import TempiLink from "@/components/TempiLink.vue";
import { TypedJSON } from "typedjson";
import { Song } from "@/model/Song";
import { SongFilter } from "@/model/SongFilter";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { DanceStats, TempoRange } from "@/model/DanceStats";

declare const environment: DanceEnvironment;

@Component({
  components: {
    TempiLink,
  },
})
export default class DanceDescription extends Vue {
  @Prop() private readonly description!: string;
  @Prop() private readonly danceId!: string;

  private get dance(): DanceStats | undefined {
    return environment?.fromId(this.danceId);
  }

  private get danceName(): string | undefined {
    return this.dance?.danceName;
  }

  private get rangeText(): string {
    const tempo = this.dance?.tempoRange;
    return tempo && tempo.min === tempo.max ? "at" : "between";
  }

  private get tempoFilter(): SongFilter {
    const tempo = this.tempoRange;
    const filter = new SongFilter();
    filter.action = "advanced";
    filter.tempoMin = tempo?.min;
    filter.tempoMax = tempo?.max;
    filter.dances = this.danceId;

    return filter;
  }

  private get tempoRange(): TempoRange | undefined {
    return this.dance?.tempoRange;
  }

  private get bpmText(): string {
    const tempo = this.tempoRange;
    return `${tempo ? tempo.toString(" and ") : ""} beats per minute`;
  }

  private get mpmText(): string {
    const tempo = this.tempoRange;
    const numerator = this.dance?.dance.meter.numerator!;
    return `${tempo ? tempo.mpm(numerator, " and ") : ""} measures per minute`;
  }
}
</script>
