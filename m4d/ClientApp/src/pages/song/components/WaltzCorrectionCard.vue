<script setup lang="ts">
import { computed } from "vue";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { PropertyType } from "@/models/SongProperty";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";

const WALTZ_IDS: string[] = safeDanceDatabase().groups.find((g) => g.id === "WLZ")?.danceIds ?? [];

const props = defineProps<{
  song: Song;
  editor?: SongEditor;
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

const correctedTempo = computed(() =>
  props.song.tempo ? Math.round((props.song.tempo * 3) / 4) : undefined,
);

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

const onBadMeterAndTempo = () => {
  applyMeterFix();
  props.editor!.modifyProperty(PropertyType.tempoField, correctedTempo.value!.toString());
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
      <div class="d-grid gap-2">
        <BButton variant="outline-secondary" @click="onFake">
          Performer dances Waltz to 4/4 (mark as Fake)
        </BButton>
        <BButton variant="outline-warning" @click="onBadMeter">
          Meter tag wrong — correct 4/4 → 3/4 (tempo unchanged)
        </BButton>
        <BButton v-if="correctedTempo" variant="outline-danger" @click="onBadMeterAndTempo">
          Meter + tempo wrong — correct 4/4 → 3/4 and adjust BPM ({{ song.tempo }} →
          {{ correctedTempo }})
        </BButton>
        <BButton variant="outline-info" @click="onCompoundTime">
          Song has compound time — 4/4 feel (e.g. Foxtrot) with underlying waltz triple feel (adds
          12/8 and marks waltz as Compound Time)
        </BButton>
      </div>
    </BCardBody>
  </BCard>
</template>
