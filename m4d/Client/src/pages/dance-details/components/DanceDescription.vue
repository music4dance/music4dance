<template>
  <div>
    <mark-down-editor
      v-model="descriptionInternal"
      :editing="editing"
      ref="description"
    >
    </mark-down-editor>
    <div id="tempo-info" v-if="!dance.isGroup && numerator != 1">
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
import MarkDownEditor from "@/components/MarkDownEditor.vue";
import TempiLink from "@/components/TempiLink.vue";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceStats } from "@/model/DanceStats";
import { Editor } from "@/model/Editor";
import { SongFilter } from "@/model/SongFilter";
import { TempoRange } from "@/model/TempoRange";
import { TypeStats } from "@/model/TypeStats";
import "reflect-metadata";

export default EnvironmentManager.extend({
  components: { MarkDownEditor, TempiLink },
  props: {
    description: { type: String, required: true },
    danceId: { type: String, required: true },
    editing: Boolean,
  },
  data() {
    return new (class {})();
  },
  computed: {
    descriptionInternal: {
      get: function (): string {
        return this.description;
      },
      set: function (value: string): void {
        this.$emit("input", value);
      },
    },
    isModified(): boolean {
      return this.editor.isModified;
    },
    editor(): Editor {
      return this.$refs.description as unknown as Editor;
    },

    dance(): DanceStats | undefined {
      return this.environment.fromId(this.danceId);
    },

    danceName(): string | undefined {
      return this.dance?.name;
    },

    rangeText(): string {
      const tempo = this.typeStats?.tempoRange;
      return tempo && tempo.min === tempo.max ? "at" : "between";
    },

    tempoFilter(): SongFilter {
      const tempo = this.tempoRange;
      const filter = new SongFilter();
      filter.action = "advanced";
      filter.tempoMin = tempo?.min;
      filter.tempoMax = tempo?.max;
      filter.dances = this.danceId;

      return filter;
    },

    tempoRange(): TempoRange | undefined {
      return this.typeStats?.tempoRange;
    },

    bpmText(): string {
      const tempo = this.tempoRange;
      return `${tempo ? tempo.toString(" and ") : ""} beats per minute`;
    },

    numerator(): number {
      return this.typeStats?.meter.numerator ?? 0;
    },

    mpmText(): string {
      const tempo = this.tempoRange;
      const numerator = this.numerator;
      return `${
        tempo ? tempo.mpm(numerator, " and ") : ""
      } measures per minute`;
    },

    typeStats(): TypeStats | undefined {
      if (this.dance?.isGroup) {
        throw new Error(
          `Attempted to find tempo for a group: ${this.dance?.name}`
        );
      }
      return this.dance as TypeStats;
    },
  },
  methods: {
    commit(): void {
      this.editor.commit();
    },
  },
});
</script>
