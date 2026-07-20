<script setup lang="ts">
import { computed } from "vue";
import type { TableFieldRaw } from "bootstrap-vue-next";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { PropertyType } from "@/models/SongProperty";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const WALTZ_IDS: string[] = safeDanceDatabase().groups.find((g) => g.id === "WLZ")?.danceIds ?? [];

interface TempoCorrectionCase {
  id: string;
  label: string;
  numerator: number;
  denominator: number;
}

// What the tempo algorithm likely did wrong. Add a case here to support a
// newly-observed miscount pattern — no other code changes are needed.
const TEMPO_CORRECTION_CASES: TempoCorrectionCase[] = [
  { id: "4-to-3", label: "Counted 4 beats/measure instead of 3", numerator: 3, denominator: 4 },
  { id: "2-to-3", label: "Counted 2 beats/measure instead of 3", numerator: 3, denominator: 2 },
  { id: "div-3", label: "Tripled the true tempo", numerator: 1, denominator: 3 },
  { id: "mul-3", label: "Reported one-third of the true tempo", numerator: 3, denominator: 1 },
  { id: "double", label: "Reported half the true tempo", numerator: 2, denominator: 1 },
  { id: "halve", label: "Reported double the true tempo", numerator: 1, denominator: 2 },
];

interface TempoCorrectionOption extends TempoCorrectionCase {
  correctedTempo: number;
  math: string;
}

const props = defineProps<{
  song: Song;
  editor: SongEditor;
  editing?: boolean;
  user?: string;
  canEdit?: boolean;
}>();

const emit = defineEmits<{ edit: [] }>();

const waltzRatings = computed(() =>
  (props.song.danceRatings ?? []).filter((dr) => WALTZ_IDS.includes(dr.danceId) && dr.weight > 0),
);

const showCard = computed(
  () =>
    !!props.user &&
    props.canEdit &&
    !props.editing &&
    waltzRatings.value.length > 0 &&
    props.song.hasMeterTag(4),
);

const tempoCorrectionOptions = computed<TempoCorrectionOption[]>(() => {
  const tempo = props.song.tempo;
  if (tempo == null) return [];
  return TEMPO_CORRECTION_CASES.map((c) => {
    const correctedTempo = Math.round((tempo * c.numerator) / c.denominator);
    return {
      ...c,
      correctedTempo,
      math: `${tempo} × ${c.numerator}/${c.denominator} = ${correctedTempo} BPM`,
    };
  });
});

const tempoCorrectionFields: Exclude<TableFieldRaw<TempoCorrectionOption>, string>[] = [
  { key: "label", label: "What likely happened" },
  { key: "math", label: "Correction" },
  { key: "action", label: "" },
];

const applyMeterFix = () => {
  props.editor!.addProperty(PropertyType.removedTags, "4/4:Tempo");
  if (!props.song.hasMeterTag(3)) {
    props.editor!.addProperty(PropertyType.addedTags, "3/4:Tempo");
  }
};

const onFake = () => {
  for (const dr of waltzRatings.value) {
    props.editor!.addProperty(`${PropertyType.addedTags}:${dr.danceId}`, "Fake:Tempo");
  }
  emit("edit");
};

const onBadMeter = () => {
  applyMeterFix();
  emit("edit");
};

const onTempoCorrection = (opt: TempoCorrectionOption) => {
  applyMeterFix();
  props.editor!.modifyProperty(PropertyType.tempoField, opt.correctedTempo.toString());
  emit("edit");
};

const onCompoundTime = () => {
  // Add 12/8 song-level meter tag only if not already present
  if (!props.song.tags?.some((t) => t.key === "12/8:Tempo")) {
    props.editor!.addProperty(PropertyType.addedTags, "12/8:Tempo");
  }
  // Mark each waltz dance rating as compound time
  for (const dr of waltzRatings.value) {
    props.editor!.addProperty(`${PropertyType.addedTags}:${dr.danceId}`, "Compound Time:Tempo");
  }
  emit("edit");
};
</script>

<template>
  <BCard v-if="showCard" border-variant="warning" class="mt-2">
    <template #header>
      <span class="text-warning fw-semibold">Waltz / 4/4 Conflict</span>
    </template>
    <BCardBody>
      <p class="mb-2 small text-muted">
        This song is rated as a Waltz (3/4 meter) but has a <strong>4/4</strong> meter tag. Choose
        the appropriate correction:
      </p>
      <div class="d-grid gap-2 mb-3">
        <BButton variant="outline-secondary" @click="onFake">
          Performer dances Waltz to 4/4 (mark as Fake)
        </BButton>
        <BButton variant="outline-warning" @click="onBadMeter">
          Meter tag wrong — correct 4/4 → 3/4 (tempo unchanged)
        </BButton>
      </div>
      <template v-if="tempoCorrectionOptions.length">
        <p class="mb-2 small text-muted">
          Meter <strong>and</strong> tempo wrong — count the beat by hand, then apply whichever
          corrected tempo matches:
        </p>
        <BTable
          small
          borderless
          :items="tempoCorrectionOptions"
          :fields="tempoCorrectionFields"
          class="mb-3"
        >
          <template #cell(action)="{ item }">
            <BButton size="sm" variant="outline-danger" @click="onTempoCorrection(item)">
              Apply
            </BButton>
          </template>
        </BTable>
      </template>
      <div class="d-grid gap-2">
        <BButton variant="outline-info" @click="onCompoundTime">
          Song has compound time — 4/4 feel (e.g. Foxtrot) with underlying waltz triple feel (adds
          12/8 and marks waltz as Compound Time)
        </BButton>
      </div>
    </BCardBody>
  </BCard>
</template>
