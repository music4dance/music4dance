<template>
  <div>
    <mark-down-editor
      v-model="descriptionInternal"
      :editing="editing"
      ref="description"
    >
    </mark-down-editor>
    <div id="tempo-info" v-if="!dance.isGroup && dance.meter.numerator != 1">
      <h2>Tempo Information</h2>
      <p>
        The {{ danceName }} is generally danced to music in a
        {{ dance.meter.toString() }} meter {{ rangeText }} {{ bpmText }} ({{
          mpmText
        }}). <a :href="tempoFilter.url">Click here</a> to see a list of
        {{ danceName }} songs {{ rangeText }} {{ mpmText }}.
      </p>
      <tempi-link></tempi-link>
    </div>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import MarkDownEditor from "@/components/MarkDownEditor.vue";
import TempiLink from "@/components/TempiLink.vue";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { DanceStats, TempoRange, TypeStats } from "@/model/DanceStats";
import { SongFilter } from "@/model/SongFilter";
import { Editor } from "@/model/Editor";

@Component({
  components: {
    MarkDownEditor,
    TempiLink,
  },
})
export default class DanceDescription
  extends Mixins(EnvironmentManager)
  implements Editor
{
  @Prop() private readonly description!: string;
  @Prop() private readonly danceId!: string;
  @Prop() private readonly editing!: boolean;

  public get isModified(): boolean {
    return this.editor.isModified;
  }

  public commit(): void {
    this.editor.commit();
  }

  private get editor(): Editor {
    return this.$refs.description as unknown as Editor;
  }

  private get dance(): DanceStats | undefined {
    return this.environment.fromId(this.danceId);
  }

  private get danceName(): string | undefined {
    return this.dance?.name;
  }

  private get descriptionInternal(): string {
    return this.description;
  }

  private set descriptionInternal(value: string) {
    this.$emit("input", value);
  }

  private get rangeText(): string {
    const tempo = this.typeStats?.tempoRange;
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
    return this.typeStats?.tempoRange;
  }

  private get bpmText(): string {
    const tempo = this.tempoRange;
    const numerator = this.typeStats?.meter.numerator ?? 0;
    return `${tempo ? tempo.bpm(numerator, " and ") : ""} beats per minute`;
  }

  private get mpmText(): string {
    const tempo = this.tempoRange;
    return `${tempo ? tempo.toString(" and ") : ""} measures per minute`;
  }

  private get typeStats(): TypeStats | undefined {
    if (this.dance?.isGroup) {
      throw new Error(
        `Attempted to find tempo for a group: ${this.dance?.name}`
      );
    }
    return this.dance as TypeStats;
  }
}
</script>
