<script setup lang="ts">
import { Song } from "@/models/Song";
import { PropertyType, SongProperty } from "@/models/SongProperty";
import { computed } from "vue";
import { formatDate } from "@/helpers/timeHelpers";
import { getMenuContext } from "@/helpers/GetMenuContext";

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  song: Song;
  editing?: boolean;
  isCreator?: boolean;
  user?: string;
}>();

const context = getMenuContext();
const isSystemTempo = computed(
  () => !!props.song.tempo && !props.song.isUserModified(PropertyType.tempoField),
);
const canOverrideTempo = computed(() => !!props.user && isSystemTempo.value);
/** Any signed-in user can set tempo when none has been recorded yet, or re-edit it if they were the last human to set it (tracked via `Song.propLastSetBy`). */
const canSetTempo = computed(
  () =>
    !!props.user &&
    (!props.song.tempo || props.song.propLastSetBy(PropertyType.tempoField) === props.user),
);
/** Combined: user can interact with the tempo pencil (algo override or initial set). */
const canEditTempo = computed(() => canOverrideTempo.value || canSetTempo.value);
/** canTag users have elevated privileges and can edit tempo on any song. */
const canTagUser = computed(() => !!props.user && context.hasRole("canTag"));
const isInferredTempo = computed(() => !!props.song.tempo && props.song.isTempoInferredFromDance);
const tempoEditorValue = computed(() =>
  props.editing && isInferredTempo.value ? "" : props.song.tempo ? props.song.tempo.toString() : "",
);
const tempoPlaceholder = computed(() =>
  props.editing && isInferredTempo.value && props.song.tempo
    ? `${props.song.tempo} (inferred)`
    : undefined,
);

const modifiedFormatted = computed(() => formatDate(props.song.modified));
const createdFormatted = computed(() => formatDate(props.song.created));
const editedFormatted = computed(() => formatDate(props.song.edited!));

const emit = defineEmits<{
  edit: [targetSelector?: string];
  "update-field": [field: SongProperty];
}>();

const formatEchoNest = (n: number): string => {
  return (n * 100).toFixed(1).toString() + "%";
};
</script>

<template>
  <BTableSimple borderless small>
    <BTr v-if="!!song.length || editing">
      <BTh>Length</BTh>
      <BTd
        ><FieldEditor
          name="Length"
          :value="song.length ? song.length.toString() : ''"
          input-width="10.5em"
          :editing="editing"
          :is-creator="isCreator"
          role="canTag"
          type="number"
          v-bind="$attrs"
        />
        Seconds</BTd
      >
    </BTr>
    <BTr v-if="!!song.tempo || canEditTempo || canTagUser || editing">
      <BTh>Tempo</BTh>
      <BTd
        ><FieldEditor
          name="Tempo"
          :value="tempoEditorValue"
          :placeholder="tempoPlaceholder"
          input-width="10.5em"
          input-class="tempo-input-wide tempo-inferred"
          :editing="editing"
          :is-creator="isCreator"
          role="canTag"
          :override-permission="canEditTempo"
          type="number"
          v-bind="$attrs"
          >{{ song.tempo || "???" }}</FieldEditor
        >
        BPM<AlgoGeneratedIcon v-if="!editing" :song="song" /><BButton
          v-if="(canEditTempo || canTagUser) && !editing"
          type="button"
          variant="link"
          class="ms-1 p-0 align-baseline"
          @click="emit('edit', 'input[data-field-name=&quot;Tempo&quot;]')"
          ><IBiPencilFill /></BButton
        ><BButton
          v-if="canTagUser && editing && song.tempo"
          type="button"
          variant="link"
          class="ms-1 p-0 align-baseline text-danger"
          title="Clear tempo"
          @click="$emit('update-field', new SongProperty({ name: 'Tempo', value: '' }))"
          ><IBiXCircle /></BButton></BTd
    ></BTr>
    <BTr v-if="song.danceability">
      <BTh>Beat</BTh>
      <BTd>
        <EchoIcon
          :value="song.danceability"
          type="beat"
          label="beat strength"
          max-label="strongest beat"
        />
        {{ formatEchoNest(song.danceability) }}</BTd
      >
    </BTr>
    <BTr v-if="song.energy">
      <BTh>Energy</BTh>
      <BTd>
        <EchoIcon
          :value="song.energy"
          type="energy"
          label="energy level"
          max-label="highest energy"
        />
        {{ formatEchoNest(song.energy) }}</BTd
      >
    </BTr>
    <BTr v-if="song.valence">
      <BTh>Mood</BTh>
      <BTd>
        <EchoIcon :value="song.valence" type="mood" label="mood level" max-label="happiest" />
        {{ formatEchoNest(song.valence) }}</BTd
      >
    </BTr>
    <BTr v-if="song.created">
      <BTh>Created</BTh>
      <BTd>{{ createdFormatted }}</BTd>
    </BTr>
    <BTr v-if="song.modified">
      <BTh>Modified</BTh>
      <BTd>{{ modifiedFormatted }}</BTd>
    </BTr>
    <BTr v-if="song.edited">
      <BTh>Edited</BTh>
      <BTd>{{ editedFormatted }}</BTd>
    </BTr>
  </BTableSimple>
</template>

<style scoped>
.tempo-input-wide {
  width: 10.5em;
}

.tempo-inferred::placeholder {
  color: var(--bs-secondary-color);
  font-style: italic;
  opacity: 0.85;
}
</style>
