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

const dance = computed(() => danceDB.fromId(props.danceId)!);
const isGroup = computed(() => DanceGroup.isGroup(dance.value));
const danceType = computed(() => (isGroup.value ? undefined : (dance.value as DanceType)));
const danceName = computed(() => dance.value.name);
const tempoRange = computed(() => danceType.value?.tempoRange);
const rangeText = computed(() =>
  tempoRange.value && tempoRange.value.min === tempoRange.value.max ? "at" : "between",
);
const tempoFilter = computed(() => {
  const filter = new SongFilter();
  filter.action = "advanced";
  filter.tempoMin = tempoRange.value?.min;
  filter.tempoMax = tempoRange.value?.max;
  filter.dances = props.danceId;
  return filter;
});
const meter = computed(() => danceType.value?.meter);
const numerator = computed(() => meter.value?.numerator);
const bpmText = computed(
  () => `${tempoRange.value ? tempoRange.value.toString(" and ") : ""} beats per minute`,
);
const mpmText = computed(
  () =>
    `${tempoRange.value && danceType.value ? tempoRange.value.mpm(danceType.value.meter.numerator, " and ") : ""} measures per minute`,
);

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
    <MarkDownEditor
      ref="markdownEditor"
      v-model="model"
      :editing="editing"
      :dance-name="danceName"
    />
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
