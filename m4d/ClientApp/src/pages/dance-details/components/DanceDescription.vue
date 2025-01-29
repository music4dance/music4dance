<script setup lang="ts">
import MarkDownEditor from "@/components/MarkDownEditor.vue";
import { DanceType } from "@/models/DanceDatabase/DanceType";
import { SongFilter } from "@/models/SongFilter";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import { computed, ref } from "vue";

const danceDB = safeDanceDatabase();

const props = defineProps<{
  danceId: string;
  editing: boolean;
}>();

const model = defineModel<string>({ required: true });
const markdownEditor = ref<InstanceType<typeof MarkDownEditor> | null>(null);

const dance = danceDB.fromId(props.danceId)!;
const isGroup = DanceGroup.isGroup(dance);
const danceType = isGroup ? undefined : (dance as DanceType);
const danceName = dance.name;
const tempoRange = danceType?.tempoRange;
const rangeText = tempoRange && tempoRange.min === tempoRange.max ? "at" : "between";
const tempoFilter = (() => {
  const filter = new SongFilter();
  filter.action = "advanced";
  filter.tempoMin = tempoRange?.min;
  filter.tempoMax = tempoRange?.max;
  filter.dances = props.danceId;
  return filter;
})();
const meter = danceType?.meter;
const numerator = meter?.numerator;
const bpmText = `${tempoRange ? tempoRange.toString(" and ") : ""} beats per minute`;
const mpmText = `${tempoRange ? tempoRange.mpm(danceType.meter.numerator, " and ") : ""} measures per minute`;

const commit = (): void => {
  markdownEditor.value?.commit();
};

const isModified = computed(() => markdownEditor.value?.isModified);

defineExpose({
  commit,
  isModified,
});
</script>

<template>
  <div>
    <MarkDownEditor ref="markdownEditor" v-model="model" :editing="editing" />
    <div v-if="!isGroup && numerator != 1" id="tempo-info">
      <h2>Tempo Information</h2>
      <p>
        The {{ danceName }} is generally danced to music in a {{ meter!.toString() }} meter
        {{ rangeText }} {{ bpmText }} ({{ mpmText }}). <a :href="tempoFilter.url">Click here</a> to
        see a list of {{ danceName }} songs {{ rangeText }} {{ mpmText }}.
      </p>
      <TempiLink />
    </div>
  </div>
</template>
