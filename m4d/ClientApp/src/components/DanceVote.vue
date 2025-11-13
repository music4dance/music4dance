<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, ref, useId } from "vue";

const danceDB = safeDanceDatabase();
const uniqueId = useId();

const props = defineProps<{
  vote?: boolean;
  danceRating: DanceRating;
  authenticated?: boolean;
  filterStyleTag?: string; // Style tag from search context (e.g., "American", "International")
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const showStyleChoiceModal = ref<boolean>(false);

const danceId = computed(() => props.danceRating.danceId);
const dance = computed(() => danceDB.fromId(danceId.value)!);

const styleFamilies = computed(() => danceDB.getStyleFamilies(danceId.value));
const hasSingleStyle = computed(() => styleFamilies.value.length === 1);
const hasMultipleStyles = computed(() => styleFamilies.value.length > 1);

console.log("DanceVote initialized:", {
  danceId: danceId.value,
  filterStyleTag: props.filterStyleTag,
  styleFamilies: styleFamilies.value,
  hasSingleStyle: hasSingleStyle.value,
  hasMultipleStyles: hasMultipleStyles.value,
});

const pendingVote = ref<VoteDirection | undefined>(undefined);
const modalId = `style-choice-modal-${uniqueId}`;

const handleVote = (direction: VoteDirection): void => {
  console.log("Handling vote", direction);
  console.log("State:", {
    hasSingleStyle: hasSingleStyle.value,
    hasMultipleStyles: hasMultipleStyles.value,
    filterStyleTag: props.filterStyleTag,
    styleFamilies: styleFamilies.value,
    modalId: modalId,
  });
  // If single style or has filter style tag, vote immediately
  if (hasSingleStyle.value) {
    console.log("Single style detected:", styleFamilies.value[0]);
    emitVote(direction, styleFamilies.value[0]);
  } else if (props.filterStyleTag) {
    console.log("Using filter style tag:", props.filterStyleTag);
    emitVote(direction, props.filterStyleTag);
  } else if (hasMultipleStyles.value) {
    console.log("Multiple styles detected, showing style choice modal:", modalId);
    // Show modal to select style
    pendingVote.value = direction;
    showStyleChoiceModal.value = true;
  } else {
    console.log("No style choice needed");
    // No style tag needed
    emitVote(direction, undefined);
  }
};

const onStyleSelected = (styleTag: string): void => {
  if (pendingVote.value !== undefined) {
    emitVote(pendingVote.value, styleTag);
    pendingVote.value = undefined;
  }
};

const emitVote = (direction: VoteDirection, styleTag?: string): void => {
  console.log("Emitting dance-vote event:", { danceId: danceId.value, direction, styleTag });
  emit("dance-vote", new DanceRatingVote(danceId.value, direction, styleTag));
};

const upVote = () => handleVote(VoteDirection.Up);
const downVote = () => handleVote(VoteDirection.Down);
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
    <StyleChoiceModal
      v-if="hasMultipleStyles"
      :id="modalId"
      v-model="showStyleChoiceModal"
      :styles="styleFamilies"
      :dance-name="dance.name"
      @style-selected="onStyleSelected"
    />
  </div>
</template>
