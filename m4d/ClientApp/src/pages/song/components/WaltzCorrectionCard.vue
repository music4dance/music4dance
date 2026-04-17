<script setup lang="ts">
import { computed } from "vue";
import { Song } from "@/models/Song";
import { SongEditor } from "@/models/SongEditor";
import { PropertyType } from "@/models/SongProperty";

const WALTZ_IDS = ["SWZ", "CSW", "VWZ", "TGV"] as const;

const props = defineProps<{
  song: Song;
  editor?: SongEditor;
  editing?: boolean;
  user?: string;
  canEdit?: boolean;
}>();

const emit = defineEmits<{ edit: [] }>();

const waltzRatings = computed(() =>
  (props.song.danceRatings ?? []).filter(
    (dr) => (WALTZ_IDS as readonly string[]).includes(dr.danceId) && dr.weight > 0,
  ),
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
      </div>
    </BCardBody>
  </BCard>
</template>
