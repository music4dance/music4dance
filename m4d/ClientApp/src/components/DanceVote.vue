<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, ref } from "vue";

const danceDB = safeDanceDatabase();

const props = defineProps<{
  vote?: boolean;
  danceRating: DanceRating;
  authenticated?: boolean;
  filterStyleTag?: string; // Style tag from search context (e.g., "American", "International")
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const danceId = computed(() => props.danceRating.danceId);
const dance = computed(() => danceDB.fromId(danceId.value)!);

const styleFamilies = computed(() => danceDB.getStyleFamilies(danceId.value));
const hasSingleStyle = computed(() => styleFamilies.value.length === 1);
const hasMultipleStyles = computed(() => styleFamilies.value.length > 1);

// Auto-select style if: 1) only one style exists, or 2) filtered by style in search
const selectedStyle = ref<string | undefined>(
  hasSingleStyle.value ? styleFamilies.value[0] : props.filterStyleTag,
);

const upVote = () =>
  emit("dance-vote", new DanceRatingVote(danceId.value, VoteDirection.Up, selectedStyle.value));
const downVote = () =>
  emit("dance-vote", new DanceRatingVote(danceId.value, VoteDirection.Down, selectedStyle.value));
const maxWeight = computed(() => safeDanceDatabase().getMaxWeight(danceId.value));
</script>

<template>
  <div class="d-flex align-items-center gap-2">
    <DanceVoteButton
      :vote="vote"
      :value="danceRating.weight"
      :authenticated="!!authenticated"
      :dance-name="dance.name"
      :max-vote="maxWeight"
      @up-vote="upVote"
      @down-vote="downVote"
    />
    <StyleSelector
      v-if="hasMultipleStyles"
      v-model="selectedStyle"
      :styles="styleFamilies"
      size="sm"
    />
  </div>
</template>
