<script setup lang="ts">
import { computed } from "vue";
import type { ClusterChoice, NameField } from "../ClusterChoice";
import type { SharedIdReviewSong } from "../SharedIdReviewModel";

// One of a cluster's Title, Artist or Individual artists rows: a radio per song, and one in the
// If merged column for the proposal. A song outside every group (or any song, when the action
// isn't a merge) can't be picked; when the songs are split, each radio says which group it names.
const props = defineProps<{
  choice: ClusterChoice;
  field: NameField;
  // Unique on the page, for the radios' name and ids
  prefix: string;
  heading: string;
  textOf: (song: SharedIdReviewSong) => string;
  proposed?: string;
  // What the merge gives on its own, when the proposal changes it
  mergeAlone?: string;
}>();

// The proposal's radio value; null in the choice
const proposal = "merged";

const songs = computed(() => props.choice.cluster.songs);
const name = computed(() => `${props.prefix}-${props.field}`);
const picked = computed(() => props.choice.names[props.field] ?? proposal);
const hasProposal = computed(() => props.choice.cluster.proposal !== undefined);

function onPick(value: unknown): void {
  props.choice.pickName(props.field, value === proposal ? null : String(value));
}
</script>

<template>
  <tr>
    <th>{{ heading }}</th>
    <td v-for="song in songs" :key="song.songId">
      <BFormRadio
        :id="`${name}-${song.songId}`"
        :name="name"
        :value="song.songId"
        :model-value="picked"
        :disabled="!choice.canName(song.songId)"
        @update:model-value="onPick"
      >
        {{ textOf(song) }}
        <BBadge
          v-if="choice.isSplit && choice.canName(song.songId)"
          variant="light"
          class="ms-1 border"
          :title="`Names group ${choice.groupOf(song.songId)}`"
          >{{ choice.groupOf(song.songId) }}</BBadge
        >
      </BFormRadio>
    </td>
    <td class="table-info">
      <template v-if="hasProposal">
        <BFormRadio
          :id="`${name}-merged`"
          :name="name"
          :value="proposal"
          :model-value="picked"
          :disabled="!choice.isMerge"
          @update:model-value="onPick"
        >
          {{ proposed }}
        </BFormRadio>
        <div v-if="mergeAlone !== undefined" class="small text-muted">
          The merge alone gives: {{ mergeAlone }}
        </div>
      </template>
    </td>
  </tr>
</template>
